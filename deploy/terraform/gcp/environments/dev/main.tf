# 1. Networking (VPC, Subnets, Cloud SQL Private Peering, Cloud NAT)
module "networking" {
  source       = "../../modules/networking"
  project_id   = var.project_id
  region       = var.region
  environment  = var.environment
  network_name = "shopsphere-vpc"
}

# 2. Private DNS (shopsphere.internal)
module "dns" {
  source              = "../../modules/dns"
  project_id          = var.project_id
  environment         = var.environment
  vpc_id              = module.networking.vpc_id
  rabbitmq_private_ip = module.rabbitmq.private_ip
}

# 3. Artifact Registry (Docker Images)
module "artifact_registry" {
  source        = "../../modules/artifact-registry"
  project_id    = var.project_id
  region        = var.region
  repository_id = "shopsphere-containers-${var.environment}"
}

# 4. IAM & Workload Identity Federation (GitHub Actions)
module "iam_wif" {
  source      = "../../modules/iam-wif"
  project_id  = var.project_id
  environment = var.environment
  github_repo = var.github_repo
}

# 5. Secret Manager (Per-service secrets)
module "secrets" {
  source      = "../../modules/secrets"
  project_id  = var.project_id
  environment = var.environment
}

# 6. Cloud SQL for SQL Server 2022 (Web Edition, Zonal)
module "cloud_sql" {
  source                        = "../../modules/cloud-sql"
  project_id                    = var.project_id
  region                        = var.region
  environment                   = var.environment
  vpc_id                        = module.networking.vpc_id
  service_networking_connection = module.networking.service_networking_connection
  database_version              = "SQLSERVER_2022_WEB"
  tier                          = "db-custom-2-7680"
  availability_type             = "ZONAL"
}

# 7. Memorystore for Redis 7 (Basic Tier, 1 GB)
module "memorystore" {
  source         = "../../modules/memorystore"
  project_id     = var.project_id
  region         = var.region
  environment    = var.environment
  vpc_id         = module.networking.vpc_id
  tier           = "BASIC"
  memory_size_gb = 1
}

# 8. RabbitMQ Compute Engine VM (IAP-only management)
resource "random_password" "rabbitmq_admin" {
  length  = 20
  special = false
}

module "rabbitmq" {
  source         = "../../modules/rabbitmq"
  project_id     = var.project_id
  region         = var.region
  zone           = var.zone
  environment    = var.environment
  vpc_id         = module.networking.vpc_id
  data_subnet_id = module.networking.data_subnet_id
  machine_type   = "e2-medium"
  disk_size_gb   = 20
  admin_password = random_password.rabbitmq_admin.result
}

# 8b. Automated Secret Versions (Populate generated connection strings into Secret Manager)
resource "google_secret_manager_secret_version" "db_conn" {
  for_each    = module.secrets.db_secret_ids
  secret      = each.value
  secret_data = module.cloud_sql.connection_strings[each.key]
  depends_on  = [module.secrets]
}

resource "google_secret_manager_secret_version" "rabbitmq_conn" {
  for_each    = module.secrets.rabbitmq_secret_ids
  secret      = each.value
  secret_data = "amqp://admin:${random_password.rabbitmq_admin.result}@rabbitmq.shopsphere.internal:5672/"
  depends_on  = [module.secrets]
}

resource "google_secret_manager_secret_version" "redis_auth" {
  secret      = module.secrets.redis_auth_secret_id
  secret_data = module.memorystore.auth_string
  depends_on  = [module.secrets]
}

# 9. Cloud Run Services: Microservices Fleet
locals {
  microservices = {
    catalog = {
      cpu    = "1"
      memory = "512Mi"
      worker = false
    }
    cart = {
      cpu    = "1"
      memory = "512Mi"
      worker = false
    }
    identity = {
      cpu    = "1"
      memory = "512Mi"
      worker = false
    }
    inventory = {
      cpu    = "1"
      memory = "1Gi"
      worker = true
    }
    payment = {
      cpu    = "1"
      memory = "512Mi"
      worker = true
    }
    order = {
      cpu    = "2"
      memory = "1Gi"
      worker = true
    }
    shipping = {
      cpu    = "1"
      memory = "512Mi"
      worker = true
    }
    notification = {
      cpu    = "1"
      memory = "512Mi"
      worker = true
    }
  }

  service_db_keys = {
    catalog      = "CatalogDatabase"
    identity     = "IdentityDatabase"
    inventory    = "InventoryDatabase"
    order        = "OrderDatabase"
    payment      = "PaymentDatabase"
    shipping     = "ShippingDb"
    notification = "NotificationDb"
  }
}

module "services" {
  for_each              = local.microservices
  source                = "../../modules/cloud-run-service"
  project_id            = var.project_id
  region                = var.region
  environment           = var.environment
  service_name          = each.key
  image                 = "${module.artifact_registry.repository_url}/${each.key}:${var.image_tag}"
  service_account_email = module.iam_wif.services_service_account_email
  ingress               = "INGRESS_TRAFFIC_INTERNAL_ONLY"
  vpc_id                = module.networking.vpc_id
  app_subnet_id         = module.networking.app_subnet_id
  min_instance_count    = each.value.worker ? 1 : 0
  cpu_idle              = !each.value.worker
  cpu                   = each.value.cpu
  memory                = each.value.memory

  invoker_members = [
    "serviceAccount:${module.iam_wif.gateway_service_account_email}",
    "allUsers"
  ]

  secret_env_vars = merge(
    each.key != "cart" ? {
      ConnectionStrings__Database                                                 = module.secrets.db_secret_ids[each.key]
      "ConnectionStrings__${lookup(local.service_db_keys, each.key, "Database")}" = module.secrets.db_secret_ids[each.key]
    } : {},
    {
      RabbitMQ__ConnectionString = module.secrets.rabbitmq_secret_ids[each.key]
    }
  )

  env_vars = merge(
    {
      ASPNETCORE_ENVIRONMENT = "Development"
      RabbitMQ__Host         = "rabbitmq.shopsphere.internal"
      RabbitMQ__Port         = "5672"
      RabbitMQ__Username     = "admin"
      RabbitMQ__Password     = random_password.rabbitmq_admin.result
    },
    contains(["cart", "catalog"], each.key) ? {
      ConnectionStrings__Redis = "${module.memorystore.host}:${module.memorystore.port},password=${module.memorystore.auth_string},ssl=True,abortConnect=False"
    } : {}
  )

  depends_on = [
    module.iam_wif,
    google_secret_manager_secret_version.db_conn,
    google_secret_manager_secret_version.rabbitmq_conn,
    google_secret_manager_secret_version.redis_auth,
  ]
}

# 10. Cloud Run Service: YARP API Gateway
module "gateway" {
  source                = "../../modules/cloud-run-service"
  project_id            = var.project_id
  region                = var.region
  environment           = var.environment
  service_name          = "gateway"
  image                 = "${module.artifact_registry.repository_url}/gateway:${var.image_tag}"
  service_account_email = module.iam_wif.gateway_service_account_email
  ingress               = "INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER"
  vpc_id                = module.networking.vpc_id
  app_subnet_id         = module.networking.app_subnet_id
  min_instance_count    = 1
  cpu_idle              = false
  cpu                   = "1"
  memory                = "512Mi"

  invoker_members = [
    "allUsers"
  ]

  env_vars = {
    ASPNETCORE_ENVIRONMENT                                                                    = "Development"
    ReverseProxy__Clusters__catalog-cluster__Destinations__catalog-service__Address           = module.services["catalog"].service_url
    ReverseProxy__Clusters__cart-cluster__Destinations__cart-service__Address                 = module.services["cart"].service_url
    ReverseProxy__Clusters__identity-cluster__Destinations__identity-service__Address         = module.services["identity"].service_url
    ReverseProxy__Clusters__inventory-cluster__Destinations__inventory-service__Address       = module.services["inventory"].service_url
    ReverseProxy__Clusters__payment-cluster__Destinations__payment-service__Address           = module.services["payment"].service_url
    ReverseProxy__Clusters__order-cluster__Destinations__order-service__Address               = module.services["order"].service_url
    ReverseProxy__Clusters__shipping-cluster__Destinations__shipping-service__Address         = module.services["shipping"].service_url
    ReverseProxy__Clusters__notification-cluster__Destinations__notification-service__Address = module.services["notification"].service_url
  }

  depends_on = [
    module.iam_wif,
  ]
}

# 11. Cloud Run Jobs: EF Core Migration Runners
module "migration_jobs" {
  for_each                    = { for k, v in local.microservices : k => v if k != "cart" }
  source                      = "../../modules/cloud-run-job"
  project_id                  = var.project_id
  region                      = var.region
  environment                 = var.environment
  job_name                    = "${each.key}-migrate"
  image                       = "us-docker.pkg.dev/cloudrun/container/hello"
  service_account_email       = module.iam_wif.services_service_account_email
  vpc_id                      = module.networking.vpc_id
  app_subnet_id               = module.networking.app_subnet_id
  secret_connection_string_id = module.secrets.db_secret_ids[each.key]

  depends_on = [
    module.iam_wif,
    google_secret_manager_secret_version.db_conn,
  ]
}

# 12. External Load Balancer, Cloud Armor WAF & GCS Static React SPA
module "load_balancer" {
  source               = "../../modules/load-balancer"
  project_id           = var.project_id
  region               = var.region
  environment          = var.environment
  gateway_service_name = module.gateway.service_name
}

# 13. Monitoring & Alert Policies
module "monitoring" {
  source             = "../../modules/monitoring"
  project_id         = var.project_id
  environment        = var.environment
  notification_email = var.notification_email
}
