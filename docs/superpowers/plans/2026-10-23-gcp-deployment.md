# GCP Dev Cloud Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the complete Google Cloud Platform (GCP) **Dev environment** deployment infrastructure for ShopSphere (~$14.22/day) using multi-stage Docker containerization, modular Terraform (`deploy/terraform/gcp/`), Cloud Run serverless microservices with Direct VPC Egress, private Cloud SQL (SQL Server 2022 Web Edition Zonal) with per-service databases and logins, Memorystore Redis 7 (Basic), private RabbitMQ VM with IAP-only management access, Cloud Armor WAF, Cloud CDN + GCS static SPA hosting, and GitHub Actions CI/CD with Workload Identity Federation.

**Architecture:** ShopSphere's .NET 8 microservices and YARP API Gateway run containerized on Google Cloud Run (Gen 2) with Direct VPC Egress (`ALL_TRAFFIC`). Gateway ingress is restricted to `INTERNAL_LOAD_BALANCER` behind a Global HTTPS Load Balancer and Cloud Armor WAF. Downstream microservices accept only `INTERNAL_ONLY` ingress and authenticate incoming Gateway requests via OIDC tokens (`roles/run.invoker`). Relational persistence uses Google Cloud SQL for SQL Server 2022 Web Edition (Zonal, 2 vCPU, 7.5 GB RAM, 50 GB SSD) with 8 per-service logins and databases over private IP, with EF Core `EnableRetryOnFailure`. Messaging is maintained via a single RabbitMQ GCE VM (`e2-medium`, 20 GB disk) resolved internally via Cloud DNS (`rabbitmq.shopsphere.internal`), with port 15672 accessible solely via IAP TCP forwarding. Background workers and outbox relays run with `min_instances = 1` and `cpu_idle = false`. Static frontend assets are hosted on GCS behind Cloud CDN.

**Tech Stack:** Google Cloud Platform (Cloud Run, Cloud SQL, Memorystore, Compute Engine, Secret Manager, Cloud Armor, Cloud DNS, Cloud CDN, Cloud Storage), Terraform (Google Provider >= 5.0), Docker (.NET 8 runtime & Alpine Migration Bundles), GitHub Actions (Workload Identity Federation), MassTransit, EF Core.

**Spec:** [`docs/superpowers/specs/2026-10-23-gcp-deployment-design.md`](../specs/2026-10-23-gcp-deployment-design.md) and [`docs/requirements/04-infrastructure.md`](../../requirements/04-infrastructure.md).

## Global Constraints

- **Never `terraform apply`**: Only run `terraform plan` and `terraform validate` during automated runs; applying infrastructure requires explicit user confirmation (`AGENTS.md`).
- **Database Isolation**: Each service owns its database (`ShopSphere.Catalog`, `ShopSphere.Cart`, `ShopSphere.Identity`, `ShopSphere.Inventory`, `ShopSphere.Payments`, `ShopSphere.Orders`, `ShopSphere.Shipping`, `ShopSphere.Notifications`) with dedicated database logins and connection strings.
- **Zero Public Exposure of State**: Cloud SQL, Memorystore Redis, and RabbitMQ must have private IPs only (`ipv4_enabled = false` for public IP, VPC peering only).
- **Ingress Hardening**: Gateway ingress is `INTERNAL_LOAD_BALANCER`; downstream services are `INTERNAL_ONLY`. Direct `*.run.app` URLs must return 403.
- **Dedicated Worker Allocation**: Services executing MassTransit consumers or Outbox relays must configure `min_instances = 1` and `cpu_idle = false`.
- **Target Environment**: Scope is strictly the `dev` environment (`deploy/terraform/gcp/environments/dev/`).
- **Conventional Commits**: All commits must follow `<type>(<scope>): [Task-XX] <description>`.
- **Lint & Health**: Terraform code must pass `terraform fmt -check` and `terraform validate`. Monorepo must pass `scripts/lint.ps1`.

## Review Focus

1. **WAF Bypass Prevention**: Gateway Cloud Run service must configure `ingress = "INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER"` so requests directly hitting `*.run.app` receive HTTP 403 and cannot bypass Cloud Armor.
2. **Direct VPC Egress Route Verification**: Cloud Run services must configure `network_interfaces` with `egress = "ALL_TRAFFIC"` and Cloud NAT so internal cross-service calls stay inside the VPC while third-party API calls (e.g. payment gateway) exit via Cloud NAT.
3. **Worker Freeze Prevention**: Services with MassTransit consumers or Outbox processors (Order, Inventory, Payment, Shipping, Notification) must set `min_instance_count = 1` and `cpu_idle = false`.
4. **Cloud SQL Dev Edition & Sizing**: Dev environment uses `SQLSERVER_2022_WEB` with `availability_type = "ZONAL"` and `tier = "db-custom-2-7680"` to keep running costs under ~$4.30/day.
5. **RabbitMQ Management Port Lockdown**: Port 15672 must not be exposed to the VPC; firewall rules must strictly restrict port 15672 to Google IAP CIDR `35.235.240.0/20`.

---

### Task 1: Containerization Scaffolding for Services, Gateway & Migration Bundles

**Files:**
- Create: `deploy/docker/Dockerfile.service`
- Create: `deploy/docker/Dockerfile.gateway`
- Create: `deploy/docker/Dockerfile.migration`
- Create: `.dockerignore`

**Interfaces:**
- Produces: Standardized multi-stage container build specifications for all .NET 8 services, YARP Gateway, and self-contained EF Core migration bundles. Static Web SPA builds directly to static assets for GCS upload.

- [ ] **Step 1: Create `.dockerignore` file in repository root**

Exclude `.git`, `.vs`, `bin`, `obj`, `node_modules`, `tests`, `artifacts`, and local logs to minimize build context.

- [ ] **Step 2: Create `deploy/docker/Dockerfile.service` for .NET 8 microservices**

Multi-stage build using `mcr.microsoft.com/dotnet/sdk:8.0-alpine` as builder and `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` as runtime with non-root user `app`. Accepts build argument `SERVICE_PATH` (e.g. `src/Services/Catalog/ShopSphere.Catalog`).

- [ ] **Step 3: Create `deploy/docker/Dockerfile.gateway` for YARP Reverse Proxy**

Multi-stage build compiling `src/Gateway/ShopSphere.Gateway` with health probe endpoint configured on port 8080 and OIDC token forwarder transform.

- [ ] **Step 4: Create `deploy/docker/Dockerfile.migration` for EF Core Migration Bundles**

Multi-stage build compiling self-contained migration bundles (`dotnet ef migrations bundle --self-contained -r linux-musl-x64`) based on Alpine runtime, containing zero SDK overhead.

- [ ] **Step 5: Verify Docker builds locally**

Run: `docker build -t test-gateway -f deploy/docker/Dockerfile.gateway .`
Expected: Successfully built and tagged `test-gateway`.

- [ ] **Step 6: Commit**

```bash
git add deploy/docker/ .dockerignore
git commit -m "feat(infra): [Task-60] add production multi-stage dockerfiles for services, gateway and migration bundles"
```

---

### Task 2: Terraform Networking, Cloud DNS, IAM/WIF & Secret Manager Modules

**Files:**
- Create: `deploy/terraform/gcp/modules/networking/main.tf`
- Create: `deploy/terraform/gcp/modules/networking/variables.tf`
- Create: `deploy/terraform/gcp/modules/networking/outputs.tf`
- Create: `deploy/terraform/gcp/modules/dns/main.tf`
- Create: `deploy/terraform/gcp/modules/dns/variables.tf`
- Create: `deploy/terraform/gcp/modules/dns/outputs.tf`
- Create: `deploy/terraform/gcp/modules/iam-wif/main.tf`
- Create: `deploy/terraform/gcp/modules/iam-wif/variables.tf`
- Create: `deploy/terraform/gcp/modules/iam-wif/outputs.tf`
- Create: `deploy/terraform/gcp/modules/secrets/main.tf`
- Create: `deploy/terraform/gcp/modules/secrets/variables.tf`
- Create: `deploy/terraform/gcp/modules/secrets/outputs.tf`
- Create: `deploy/terraform/gcp/modules/artifact-registry/main.tf`
- Create: `deploy/terraform/gcp/modules/artifact-registry/variables.tf`

**Interfaces:**
- Produces: `vpc_id`, `app_subnet_id`, `data_subnet_id`, private DNS zone `shopsphere.internal`, WIF provider pool ID, artifact repository ID, and per-service Secret Manager secrets.

- [ ] **Step 1: Implement `modules/networking`**

Declare `google_compute_network`, `app_subnet` (`10.0.1.0/24`), `data_subnet` (`10.0.2.0/24`), `google_service_networking_connection` for Cloud SQL private IP peering, and `google_compute_router` with `google_compute_router_nat`.

- [ ] **Step 2: Implement `modules/dns`**

Declare `google_dns_managed_zone` (private visibility bound to VPC) for `shopsphere.internal` and DNS `A` record for `rabbitmq.shopsphere.internal`.

- [ ] **Step 3: Implement `modules/iam-wif`**

Declare `google_iam_workload_identity_pool` and `google_iam_workload_identity_pool_provider` for GitHub Actions OIDC integration, and service accounts with IAM role bindings (`roles/run.admin`, `roles/artifactregistry.writer`, `roles/cloudsql.client`).

- [ ] **Step 4: Implement `modules/secrets`**

Declare separate `google_secret_manager_secret` resources for each service:
- Database connection strings: `catalog-db-connection-string`, `order-db-connection-string`, etc.
- RabbitMQ per-service credentials: `catalog-rabbitmq-conn`, `order-rabbitmq-conn`, etc.
- Redis auth: `shopsphere-redis-auth`.
- JWT Asymmetric keys: `identity-jwt-private-key` and `identity-jwt-public-key`.

- [ ] **Step 5: Verify module syntax**

Run: `terraform -chdir=deploy/terraform/gcp/modules/networking validate`
Expected: Success.

- [ ] **Step 6: Commit**

```bash
git add deploy/terraform/gcp/modules/networking deploy/terraform/gcp/modules/dns deploy/terraform/gcp/modules/iam-wif deploy/terraform/gcp/modules/secrets deploy/terraform/gcp/modules/artifact-registry
git commit -m "feat(infra): [Task-60] terraform gcp networking, dns, wif and per-service secrets modules"
```

---

### Task 3: Terraform Cloud SQL (Web Edition, Zonal) & Memorystore Redis (Basic) Modules

**Files:**
- Create: `deploy/terraform/gcp/modules/cloud-sql/main.tf`
- Create: `deploy/terraform/gcp/modules/cloud-sql/variables.tf`
- Create: `deploy/terraform/gcp/modules/cloud-sql/outputs.tf`
- Create: `deploy/terraform/gcp/modules/memorystore/main.tf`
- Create: `deploy/terraform/gcp/modules/memorystore/variables.tf`
- Create: `deploy/terraform/gcp/modules/memorystore/outputs.tf`

**Interfaces:**
- Consumes: `vpc_id` and `service_networking_connection` from `modules/networking`.
- Produces: `sql_private_ip`, database names, and `redis_host` + `redis_port`.

- [ ] **Step 1: Implement `modules/cloud-sql` for Dev**

Declare `google_sql_database_instance`:
- `database_version = "SQLSERVER_2022_WEB"`
- `tier = "db-custom-2-7680"` (2 vCPU, 7.5 GB RAM)
- `availability_type = "ZONAL"`
- `backup_configuration`: `enabled = true`, `point_in_time_recovery_enabled = true`
- Declare 8 per-service databases: `ShopSphere.Catalog`, `ShopSphere.Cart`, `ShopSphere.Identity`, `ShopSphere.Inventory`, `ShopSphere.Payments`, `ShopSphere.Orders`, `ShopSphere.Shipping`, `ShopSphere.Notifications`
- Declare 8 per-service SQL users (`catalog_user`, `cart_user`, etc.) with random secure passwords passed to `modules/secrets`

- [ ] **Step 2: Implement `modules/memorystore` for Dev**

Declare `google_redis_instance` with `redis_version = "REDIS_7_0"`, `tier = "BASIC"`, `memory_size_gb = 1`, `authorized_network = var.vpc_id`, `auth_enabled = true`, and `transit_encryption_mode = "SERVER_AUTHENTICATION"`.

- [ ] **Step 3: Validate Terraform configurations**

Run: `terraform -chdir=deploy/terraform/gcp/modules/cloud-sql validate` and `terraform -chdir=deploy/terraform/gcp/modules/memorystore validate`
Expected: Success.

- [ ] **Step 4: Commit**

```bash
git add deploy/terraform/gcp/modules/cloud-sql deploy/terraform/gcp/modules/memorystore
git commit -m "feat(infra): [Task-61] terraform gcp cloud sql web edition zonal and memorystore basic modules"
```

---

### Task 4: Terraform RabbitMQ Dev VM Module with Cloud DNS & IAP

**Files:**
- Create: `deploy/terraform/gcp/modules/rabbitmq/main.tf`
- Create: `deploy/terraform/gcp/modules/rabbitmq/variables.tf`
- Create: `deploy/terraform/gcp/modules/rabbitmq/outputs.tf`
- Create: `deploy/terraform/gcp/modules/rabbitmq/scripts/startup.sh`

**Interfaces:**
- Consumes: `vpc_id` and `data_subnet_id` from `modules/networking`, DNS zone from `modules/dns`.
- Produces: `rabbitmq_private_ip` and `rabbitmq_host` (`rabbitmq.shopsphere.internal`).

- [ ] **Step 1: Create RabbitMQ startup script `startup.sh`**

Script provisions Erlang 26, RabbitMQ 3.13 on Ubuntu 22.04 LTS, mounts 20 GB `pd-balanced` disk, configures AMQPS (port 5671) with TLS, enables `rabbitmq_management` (port 15672), and provisions per-service users and vhosts (`/orders`, `/inventory`, `/notifications`).

- [ ] **Step 2: Implement `modules/rabbitmq`**

Declare:
- `google_compute_disk` (20 GB `pd-balanced` SSD)
- `google_compute_instance` (`e2-medium`) attached to `data_subnet`
- `google_compute_firewall` for AMQPS (5671) open only to `10.0.1.0/24` (App Subnet)
- `google_compute_firewall` for Management UI (15672) open **only** to Google IAP CIDR `35.235.240.0/20`
- DNS A record in `modules/dns` pointing `rabbitmq.shopsphere.internal` to instance private IP

- [ ] **Step 3: Validate module**

Run: `terraform -chdir=deploy/terraform/gcp/modules/rabbitmq validate`
Expected: Success.

- [ ] **Step 4: Commit**

```bash
git add deploy/terraform/gcp/modules/rabbitmq
git commit -m "feat(infra): [Task-62] terraform gcp rabbitmq dev vm module with persistent disk, private dns and iap lockdown"
```

---

### Task 5: Terraform Cloud Run Services Fleet & Database Migration Job

**Files:**
- Create: `deploy/terraform/gcp/modules/cloud-run-service/main.tf`
- Create: `deploy/terraform/gcp/modules/cloud-run-service/variables.tf`
- Create: `deploy/terraform/gcp/modules/cloud-run-service/outputs.tf`
- Create: `deploy/terraform/gcp/modules/cloud-run-job/main.tf`
- Create: `deploy/terraform/gcp/modules/cloud-run-job/variables.tf`
- Create: `deploy/terraform/gcp/modules/cloud-run-job/outputs.tf`

**Interfaces:**
- Consumes: `app_subnet_id` from `modules/networking`, per-service secrets from `modules/secrets`, and container images from `modules/artifact-registry`.
- Produces: Cloud Run service URLs, IAM invoker roles, and migration job definitions.

- [ ] **Step 1: Implement `modules/cloud-run-service`**

Declare `google_cloud_run_v2_service` with:
- `ingress`: `INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER` for Gateway; `INGRESS_TRAFFIC_INTERNAL_ONLY` for microservices
- Direct VPC Egress: `template.vpc_access.network_interfaces { network = var.vpc_id subnetwork = var.app_subnet_id }` with `egress = "ALL_TRAFFIC"`
- Worker persistence: For Order, Inventory, Payment, Shipping, Notification services, enforce `scaling.min_instance_count = 1` and `template.containers.resources.cpu_idle = false`
- `template.containers.env` referencing per-service secret keys from Secret Manager
- IAM binding granting `roles/run.invoker` to Gateway service account on all downstream services

- [ ] **Step 2: Implement `modules/cloud-run-job` for EF Core Migrations**

Declare `google_cloud_run_v2_job` running migration bundle containers inside the VPC with Direct VPC Egress, executing before traffic redirection.

- [ ] **Step 3: Validate modules**

Run: `terraform -chdir=deploy/terraform/gcp/modules/cloud-run-service validate` and `terraform -chdir=deploy/terraform/gcp/modules/cloud-run-job validate`
Expected: Success.

- [ ] **Step 4: Commit**

```bash
git add deploy/terraform/gcp/modules/cloud-run-service deploy/terraform/gcp/modules/cloud-run-job
git commit -m "feat(infra): [Task-60] terraform cloud run gen 2 with direct vpc egress, hardened ingress and migration jobs"
```

---

### Task 6: Terraform Global HTTPS Load Balancer, Cloud CDN & Cloud Armor WAF

**Files:**
- Create: `deploy/terraform/gcp/modules/load-balancer/main.tf`
- Create: `deploy/terraform/gcp/modules/load-balancer/variables.tf`
- Create: `deploy/terraform/gcp/modules/load-balancer/outputs.tf`

**Interfaces:**
- Consumes: Gateway Cloud Run service and static frontend SPA GCS bucket.
- Produces: `external_ip_address`, `ssl_certificate_id`, and `url_map_id`.

- [ ] **Step 1: Implement Cloud Armor Security Policy**

Declare `google_compute_security_policy`:
- Preconfigured WAF rules: OWASP Top 10 rules for SQLi (`sqli-v33-stable`), XSS (`xss-v33-stable`), RCE (`rce-v33-stable`), and LFI (`lfi-v33-stable`)
- Rate limiting: Max 100 requests per minute per client IP with HTTP 429 response

- [ ] **Step 2: Implement Static SPA GCS Bucket & Cloud CDN Backend Bucket**

Declare `google_storage_bucket` for static SPA assets (React 18 build), `google_compute_backend_bucket` with `enable_cdn = true`, and custom error response routing all 404s to `/index.html` for client-side routing.

- [ ] **Step 3: Implement Global HTTPS Load Balancer & Serverless NEG**

Declare:
- `google_compute_region_network_endpoint_group` (Serverless NEG targeting Gateway Cloud Run)
- `google_compute_backend_service` for Gateway with Cloud Armor policy attached
- `google_compute_url_map` routing `/api/*` to Gateway backend service and `/*` to Cloud CDN backend bucket
- `google_compute_managed_ssl_certificate` and `google_compute_target_https_proxy`

- [ ] **Step 4: Validate module**

Run: `terraform -chdir=deploy/terraform/gcp/modules/load-balancer validate`
Expected: Success.

- [ ] **Step 5: Commit**

```bash
git add deploy/terraform/gcp/modules/load-balancer
git commit -m "feat(infra): [Task-60] terraform global https load balancer, cloud armor waf and cloud cdn spa"
```

---

### Task 7: Monitoring, Dev Environment Assembler & GitHub Actions CI/CD

**Files:**
- Create: `deploy/terraform/gcp/modules/monitoring/main.tf`
- Create: `deploy/terraform/gcp/modules/monitoring/variables.tf`
- Create: `deploy/terraform/gcp/modules/monitoring/outputs.tf`
- Create: `deploy/terraform/gcp/environments/dev/backend.tf`
- Create: `deploy/terraform/gcp/environments/dev/main.tf`
- Create: `deploy/terraform/gcp/environments/dev/variables.tf`
- Create: `deploy/terraform/gcp/environments/dev/outputs.tf`
- Create: `deploy/terraform/gcp/environments/dev/terraform.tfvars.example`
- Create: `.github/workflows/deploy-gcp.yml`

**Interfaces:**
- Consumes: All modules from Tasks 2-6.
- Produces: Complete end-to-end automated deployment pipeline with monitoring and alert policies for dev.

- [ ] **Step 1: Implement `modules/monitoring`**

Declare `google_monitoring_alert_policy` resources for dev:
- Gateway 5xx rate > 1% over 5 minutes
- Cloud SQL CPU utilization > 80% for 10 minutes
- RabbitMQ unconsumed queue depth > 500 messages over 5 minutes

- [ ] **Step 2: Implement `environments/dev/backend.tf` and `main.tf`**

Configure backend with GCS prefix `shopsphere/dev`. Wire `modules/networking`, `modules/dns`, `modules/iam-wif`, `modules/secrets`, `modules/cloud-sql`, `modules/memorystore`, `modules/rabbitmq`, `modules/cloud-run-service`, `modules/cloud-run-job`, `modules/load-balancer`, and `modules/monitoring`.

- [ ] **Step 3: Implement `.github/workflows/deploy-gcp.yml`**

Workflow with stages:
1. `validate`: Run pre-commit doctor checks and linting.
2. `build-push`: Authenticate with GCP via Workload Identity Federation, build multi-stage Docker images and migration bundles, push to Google Artifact Registry.
3. `migrate`: Execute Cloud Run Migration Jobs within VPC.
4. `terraform`: Run `terraform init`, `terraform validate`, and `terraform plan`.
5. `deploy`: Deploy new container image revisions to Cloud Run services with traffic splitting.

- [ ] **Step 4: Validate dev environment with Terraform**

Run: `terraform -chdir=deploy/terraform/gcp/environments/dev init -backend=false && terraform -chdir=deploy/terraform/gcp/environments/dev validate`
Expected: Success.

- [ ] **Step 5: Verify monorepo pre-commit health and commit**

Run: `powershell -ExecutionPolicy Bypass -File scripts/doctor.ps1`
Expected: All checks pass.

```bash
git add deploy/terraform/gcp/modules/monitoring deploy/terraform/gcp/environments/dev .github/workflows/deploy-gcp.yml
git commit -m "feat(infra): [Task-63] terraform monitoring alerts, dev environment assembler and github actions cicd"
```
