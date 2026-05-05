terraform {
  required_version = ">= 1.15.1"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }

  # State stored in Azure Storage — NEVER in the repo
  # backend "azurerm" {
  #   resource_group_name  = "rg-terraform-state"
  #   storage_account_name = "tfstateunique123"
  #   container_name       = "tfstate"
  #   key                  = "infra.terraform.tfstate"
  # }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
}

variable "environment" {
  type        = string
  description = "Environment name (dev, staging, prod)"
}

variable "location" {
  type        = string
  description = "Azure region"
  default     = "eastus2"
}

# Placeholder — add your resources below
# module "networking" {
#   source = "./modules/networking"
#   ...
# }

output "environment" {
  value = var.environment
}
