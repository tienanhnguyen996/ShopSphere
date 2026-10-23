# Design Document: ShopSphere GCP Cloud Deployment Architecture (Dev Environment)

**Date**: 2026-10-23  
**Status**: Ready for Implementation  
**Target Platform**: Google Cloud Platform (GCP) — **Dev Environment Focused**  
**Estimated Cost**: **~$14.22 / day** (~$425 / month if running continuously)  
**Spec Document**: `docs/specs/gcp-deployment-spec.md` (and this file: `docs/superpowers/specs/2026-10-23-gcp-deployment-design.md`)

---

## 1. Intent & Purpose

This design defines a cost-effective, secure, and fully functional **Dev environment** on **Google Cloud Platform (GCP)** for the ShopSphere e-commerce distributed system. It eliminates production overhead (such as multi-zone HA replicas, 3-node broker clusters, and Enterprise SQL Server licenses) while maintaining strict architectural parity with ShopSphere standards from `AGENTS.md`:
1. **Strict Service & Database Isolation**: 8 independent databases and dedicated database users on a single Zonal Cloud SQL for SQL Server (Web Edition) instance.
2. **Zero Public Exposure**: Cloud SQL, Memorystore Redis, RabbitMQ, and downstream microservices accept only private VPC traffic.
3. **WAF & Ingress Integrity**: Public traffic is gated exclusively by Cloud Armor and the Global External Application Load Balancer. Gateway ingress is locked to `INTERNAL_LOAD_BALANCER` (direct `*.run.app` returns 403).
4. **Resilient Event & Saga Processing**: Background MassTransit consumers and transactional outbox relays run with dedicated CPU allocations (`cpu_idle = false`) and `min_instances = 1` to eliminate cold stalls.
5. **Asymmetric Token Security**: In-house Identity Service signs tokens using an RSA private key (RS256); downstream services verify via public key / JWKS.
6. **Cost-Effective Infrastructure as Code**: Modular Terraform targeted at `deploy/terraform/gcp/environments/dev/`.

---

## 2. Resource Mapping & Sizing (Dev Tier)

| Component | GCP Service | Dev Configuration & Sizing | Daily Cost (Est.) |
| :--- | :--- | :--- | :--- |
| **Relational Database** | **Cloud SQL for SQL Server 2022** | **Web Edition**, Zonal (`availability_type = "ZONAL"`), tier `db-custom-2-7680` (2 vCPU, 7.5 GB RAM), 50 GB SSD storage, automated daily backups + PITR. 8 isolated databases and users. | **~$4.30 / day** |
| **Distributed Cache** | **Cloud Memorystore for Redis 7** | **Basic Tier**, 1 GB capacity, `auth_enabled = true`, in-transit TLS (`transit_encryption_mode = "SERVER_AUTHENTICATION"`), private VPC access. | **~$1.18 / day** |
| **Message Broker** | **RabbitMQ on Compute Engine** | **1x `e2-medium` VM** (1 vCPU, 4 GB RAM), 20 GB `pd-balanced` persistent disk, provisioned via metadata `startup-script`. Private DNS `rabbitmq.shopsphere.internal`. Admin UI (15672) restricted to IAP TCP forwarding. | **~$0.87 / day** |
| **Serverless Compute Fleet** | **Cloud Run (Gen 2)** | 9 services (Gateway + 8 microservices). Direct VPC Egress (`ALL_TRAFFIC`). Workers and outbox relays configured with `min_instances = 1` and `cpu_idle = false`. | **~$6.09 / day** |
| **Networking & Ingress** | **VPC, Cloud NAT & External Load Balancer** | Custom VPC (`10.0.0.0/16`), App Subnet (`10.0.1.0/24`), Data Subnet (`10.0.2.0/24`), Cloud NAT gateway ($1.08/day), 1 Global HTTPS forwarding rule ($0.60/day). | **~$1.68 / day** |
| **Edge Security** | **Cloud Armor WAF** | Preconfigured OWASP Top 10 rules and IP rate limiting (100 req/min/IP). | **~$0.10 / day** |
| **Frontend Web SPA** | **Cloud Storage (GCS) + Cloud CDN** | Static SPA assets hosted in GCS bucket behind Cloud CDN backend bucket with SPA fallback to `/index.html`. | **~$0.05 / day** |
| **DNS, Secrets & Config** | **Cloud DNS & Secret Manager** | Private DNS zone `shopsphere.internal`, per-service secrets for DB connection strings and RabbitMQ credentials. | **~$0.05 / day** |
| **Observability** | **Cloud Trace & Logging** | OpenTelemetry OTLP export directly to Cloud Operations (covered by GCP free tier). | **$0.00 / day** |
| **DEV TOTAL** | | **Target Dev Architecture Running 24/7** | **~$14.32 / day** |

---

## 3. Dev Network Topology

```mermaid
flowchart TD
    subgraph Internet["Public Internet"]
        Client["Browser Client / Mobile"]
        IAPAdmin["Developer via gcloud IAP Tunnel"]
    end

    subgraph Edge["Edge Infrastructure"]
        CloudArmor["Cloud Armor WAF (Rate Limiting + OWASP)"]
        LoadBalancer["Global External HTTPS Load Balancer"]
        CloudCDN["Cloud CDN Backend Bucket"]
        GCS["Cloud Storage: Static React SPA"]
    end

    subgraph VPC["VPC (shopsphere-vpc: 10.0.0.0/16)"]
        subgraph SubnetApp["App Subnet (10.0.1.0/24)"]
            Gateway["Cloud Run: Gateway (YARP)\nIngress: INTERNAL_LOAD_BALANCER\nDirect VPC Egress: ALL_TRAFFIC"]
            Microservices["Cloud Run: Microservices Fleet (8 Services)\n- Catalog, Cart, Inventory, Payment\n- Orders, Shipping, Identity, Notifications\nIngress: INTERNAL_ONLY\nDirect VPC Egress: ALL_TRAFFIC\nmin_instances: 1, cpu_idle: false"]
        end

        subgraph SubnetData["Data Subnet (10.0.2.0/24)"]
            CloudSQL["Cloud SQL (SQL Server 2022 Web Edition)\nZonal, 2 vCPU, 7.5 GB RAM, 50 GB SSD\nPrivate IP: 10.0.2.X\n8 Isolated DBs & Logins"]
            Memorystore["Memorystore Redis 7\nBasic Tier (1 GB), Auth + TLS\nPrivate IP: 10.0.2.Y"]
            RabbitMQVM["Compute Engine VM: rabbitmq-dev\ne2-medium, 20 GB pd-balanced SSD\nPort 5671 (AMQPS)\nPort 15672 (IAP Only)"]
        end

        CloudNAT["Cloud NAT & Cloud Router\nOutbound Internet Gateway"]
        CloudDNS["Private DNS Zone: shopsphere.internal\nrabbitmq.shopsphere.internal"]
    end

    subgraph ThirdParties["External APIs"]
        PaymentMock["Payment Gateway (Stripe/Mock)"]
    end

    Client --> CloudArmor
    CloudArmor --> LoadBalancer
    LoadBalancer -->|Path: /*| CloudCDN --> GCS
    LoadBalancer -->|Path: /api/*| Gateway

    Gateway -->|Internal HTTP + OIDC Token| Microservices
    Microservices --> CloudSQL
    Microservices --> Memorystore
    Microservices --> RabbitMQVM

    Microservices --> CloudNAT --> ThirdParties
    IAPAdmin -.->|IAP TCP Tunnel (Port 15672)| RabbitMQVM
```

---

## 4. Key Subsystem Specifications

### 4.1 Cloud SQL for SQL Server (Dev Sizing & Isolation)
- **Engine**: `SQLSERVER_2022_WEB` (significantly lower license cost than Standard/Enterprise).
- **Architecture**: `availability_type = "ZONAL"`, single zone in region `us-central1` (or user-selected region).
- **Machine Tier**: `db-custom-2-7680` (2 vCPU, 7.5 GB RAM).
- **Storage**: 50 GB SSD persistent disk with automatic storage increase enabled.
- **Database Isolation**: 8 independent databases provisioned on the single instance:
  - `ShopSphere.Catalog`, `ShopSphere.Cart`, `ShopSphere.Identity`, `ShopSphere.Inventory`
  - `ShopSphere.Payments`, `ShopSphere.Orders`, `ShopSphere.Shipping`, `ShopSphere.Notifications`
- **Dedicated Users**: Each database has its own user (`catalog_user`, `cart_user`, etc.) with `db_owner` granted only to its respective database.
- **Resilience**: EF Core `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)` on all DbContexts.
- **Migrations**: Executed via self-contained **EF Core Migration Bundles** packaged into slim Alpine containers and run as Cloud Run Jobs (`modules/cloud-run-job`) inside the VPC before traffic is routed.

### 4.2 RabbitMQ Dev VM
- **Compute**: Single `e2-medium` VM running Ubuntu 22.04 LTS.
- **Storage**: 20 GB `pd-balanced` persistent disk mounted to `/var/lib/rabbitmq`.
- **Provisioning**: Automated via Compute Engine metadata `startup-script` (installs Erlang 26 + RabbitMQ 3.13, configures TLS on port 5671, enables management plugin on 15672, and provisions per-service users and vhosts).
- **Private DNS**: Cloud DNS private managed zone `shopsphere.internal` with `A` record pointing `rabbitmq.shopsphere.internal` to the VM's internal IP.
- **Access Lockdown**:
  - Port 5671 (AMQPS): Permitted only from App Subnet (`10.0.1.0/24`).
  - Port 15672 (Management UI): Strictly restricted to Google IAP CIDR (`35.235.240.0/20`). Developers connect via `gcloud compute start-iap-tunnel rabbitmq-dev 15672 --local-host-port=15672`.

### 4.3 Cloud Run Fleet & Ingress Lockdown
- **Gateway**: `ingress = "INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER"`. Direct `*.run.app` access returns 403. Public requests must transit the Load Balancer and Cloud Armor WAF.
- **Downstream Services**: `ingress = "INGRESS_TRAFFIC_INTERNAL_ONLY"`. Direct VPC Egress (`ALL_TRAFFIC`) routes inter-service traffic via the VPC.
- **Service-to-Service IAM**: Downstream services grant `roles/run.invoker` to `gateway-sa`. YARP adds Google OIDC ID tokens (`Authorization: Bearer <oidc_token>`).
- **Worker Reliability**: Order, Inventory, Payment, Shipping, Notification services set `min_instances = 1` and `cpu_idle = false` so background MassTransit consumers and Outbox relays never stall.

### 4.4 Asymmetric Authentication (RS256)
- Identity Service generates a 2048-bit RSA keypair.
- Private key is stored in Secret Manager (`identity-jwt-private-key`) and mounted into Identity Service.
- Gateway and downstream microservices validate tokens against the public key published at `/.well-known/jwks.json`.

---

## 5. Terraform Module Architecture (`deploy/terraform/gcp`)

```
deploy/terraform/gcp/
├── environments/
│   └── dev/
│       ├── backend.tf          # GCS prefix = "shopsphere/dev"
│       ├── main.tf             # Wires dev infrastructure modules
│       ├── variables.tf
│       ├── terraform.tfvars.example
│       └── outputs.tf
└── modules/
    ├── networking/             # VPC, App (10.0.1.0/24) & Data (10.0.2.0/24) subnets, Cloud NAT
    ├── dns/                    # Private DNS zone (shopsphere.internal)
    ├── iam-wif/                # GitHub Actions Workload Identity Federation & SAs
    ├── secrets/                # Per-service Secret Manager secrets
    ├── cloud-sql/              # Cloud SQL Web Edition (Zonal), 8 service DBs & users
    ├── memorystore/            # Redis 7 Basic Tier (1 GB) with TLS & Auth
    ├── rabbitmq/               # Single VM (e2-medium, 20 GB disk), startup script, IAP firewall
    ├── cloud-run-service/      # Cloud Run Gen 2 module with Direct VPC Egress & IAM
    ├── cloud-run-job/          # Cloud Run Job for EF Core migration bundles
    ├── load-balancer/          # Global HTTPS LB, Cloud Armor WAF & GCS static SPA bucket
    ├── monitoring/             # Dev alert policies (5xx errors, high CPU, queue depth)
    └── artifact-registry/      # Docker repository in GCP
```
