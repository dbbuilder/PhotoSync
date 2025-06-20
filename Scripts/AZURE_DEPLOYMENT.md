# Azure Deployment Guide for PhotoSync

## Prerequisites

1. Azure subscription with permissions to create:
   - App Service (Linux)
   - SQL Database
   - Key Vault
   - Application Insights
   - Storage Account

2. Azure CLI installed and configured
3. .NET 6.0 SDK
4. Git repository with PhotoSync code

## Azure Resource Setup

### 1. Create Resource Group
```bash
az group create --name rg-photosync-prod --location eastus
```

### 2. Create Azure SQL Database
```bash
# Create SQL Server
az sql server create \
  --name sql-photosync-prod \
  --resource-group rg-photosync-prod \
  --location eastus \
  --admin-user photosyncadmin \
  --admin-password 'YourStrongPassword123!'

# Create Database
az sql db create \
  --server sql-photosync-prod \
  --resource-group rg-photosync-prod \
  --name PhotoDB \
  --service-objective Basic

# Configure firewall for Azure services
az sql server firewall-rule create \
  --server sql-photosync-prod \
  --resource-group rg-photosync-prod \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### 3. Create App Service Plan and Web App
```bash
# Create App Service Plan (Linux)
az appservice plan create \
  --name plan-photosync-prod \
  --resource-group rg-photosync-prod \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --plan plan-photosync-prod \
  --runtime "DOTNETCORE:6.0"
```

### 4. Create Key Vault
```bash
az keyvault create \
  --name kv-photosync-prod \
  --resource-group rg-photosync-prod \
  --location eastus
```

### 5. Create Application Insights
```bash
az monitor app-insights component create \
  --app app-photosync-insights \
  --location eastus \
  --resource-group rg-photosync-prod
```

### 6. Create Storage Account
```bash
az storage account create \
  --name stphotosyncprod \
  --resource-group rg-photosync-prod \
  --location eastus \
  --sku Standard_LRS
```

## Database Configuration

### 1. Get Connection String
```bash
az sql db show-connection-string \
  --server sql-photosync-prod \
  --name PhotoDB \
  --client ado.net
```

### 2. Run Database Setup
```sql
-- Connect to Azure SQL Database using SQL Server Management Studio or Azure Data Studio
-- Run the complete Database/StoredProcedures.sql script
```

## Application Configuration

### 1. Store Secrets in Key Vault
```bash
# Database connection string
az keyvault secret set \
  --vault-name kv-photosync-prod \
  --name "ConnectionStrings--DefaultConnection" \
  --value "your-connection-string-here"

# Application Insights key
az keyvault secret set \
  --vault-name kv-photosync-prod \
  --name "ApplicationInsights--InstrumentationKey" \
  --value "your-app-insights-key"
```

### 2. Configure App Service Settings
```bash
# Set Key Vault URL
az webapp config appsettings set \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --settings "Azure__KeyVault__VaultUrl=https://kv-photosync-prod.vault.azure.net/"

# Set folder paths for Linux
az webapp config appsettings set \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --settings "PhotoSettings__ImportFolder=/home/site/wwwroot/import"

az webapp config appsettings set \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --settings "PhotoSettings__ExportFolder=/home/site/wwwroot/export"

# Set environment
az webapp config appsettings set \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --settings "ASPNETCORE_ENVIRONMENT=Production"
```

### 3. Enable Managed Identity
```bash
az webapp identity assign \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod

# Grant Key Vault access
az keyvault set-policy \
  --name kv-photosync-prod \
  --object-id $(az webapp identity show --name app-photosync-prod --resource-group rg-photosync-prod --query principalId -o tsv) \
  --secret-permissions get list
```

## Deployment

### 1. Prepare Application for Deployment
```bash
# In your local PhotoSync directory
dotnet publish --configuration Release --runtime linux-x64 --self-contained false
```

### 2. Deploy using Azure CLI
```bash
# Create deployment package
cd bin/Release/net6.0/publish
zip -r ../photosync-deploy.zip .

# Deploy to App Service
az webapp deployment source config-zip \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --src ../photosync-deploy.zip
```

### 3. Alternative: Deploy from GitHub
```bash
# Configure GitHub deployment
az webapp deployment source config \
  --name app-photosync-prod \
  --resource-group rg-photosync-prod \
  --repo-url https://github.com/your-username/photosync \
  --branch main \
  --manual-integration
```

## File Storage Setup

### 1. Create Storage Containers
```bash
# Get storage account key
STORAGE_KEY=$(az storage account keys list --account-name stphotosyncprod --resource-group rg-photosync-prod --query '[0].value' -o tsv)

# Create containers
az storage container create --name import --account-name stphotosyncprod --account-key $STORAGE_KEY
az storage container create --name export --account-name stphotosyncprod --account-key $STORAGE_KEY
```

### 2. Configure Storage Connection
```bash
# Add storage connection string to Key Vault
STORAGE_CONNECTION="DefaultEndpointsProtocol=https;AccountName=stphotosyncprod;AccountKey=$STORAGE_KEY;EndpointSuffix=core.windows.net"

az keyvault secret set \
  --vault-name kv-photosync-prod \
  --name "Azure--Storage--ConnectionString" \
  --value "$STORAGE_CONNECTION"
```

## Monitoring Setup

### 1. Configure Application Insights
```bash
# Get Application Insights instrumentation key
INSIGHTS_KEY=$(az monitor app-insights component show --app app-photosync-insights --resource-group rg-photosync-prod --query instrumentationKey -o tsv)

# Update Key Vault with instrumentation key
az keyvault secret set \
  --vault-name kv-photosync-prod \
  --name "ApplicationInsights--InstrumentationKey" \
  --value "$INSIGHTS_KEY"
```

### 2. Set up Alerts
```bash
# Create alert for application errors
az monitor metrics alert create \
  --name "PhotoSync High Error Rate" \
  --resource-group rg-photosync-prod \
  --scopes $(az webapp show --name app-photosync-prod --resource-group rg-photosync-prod --query id -o tsv) \
  --condition "avg exceptions/server > 5" \
  --description "Alert when error rate is high"
```

## Validation

### 1. Test Deployment
```bash
# Check application status
az webapp browse --name app-photosync-prod --resource-group rg-photosync-prod

# Test API endpoints
curl https://app-photosync-prod.azurewebsites.net/api/status
```

### 2. Test Console Commands
```bash
# SSH into App Service (if enabled)
# Or use Azure Cloud Shell with deployment credentials

# Test configuration
dotnet PhotoSync.dll test

# Test import (after uploading files to storage)
dotnet PhotoSync.dll import
```

## Maintenance

### 1. Update Application
```bash
# Redeploy after code changes
dotnet publish --configuration Release --runtime linux-x64
# ... repeat deployment steps
```

### 2. Monitor Performance
- Use Application Insights dashboard
- Monitor SQL Database performance metrics
- Check App Service logs

### 3. Backup Strategy
- SQL Database automatic backups enabled
- Key Vault soft delete enabled
- Storage account geo-redundancy

## Security Considerations

1. **Network Security**:
   - Configure App Service to use VNet integration
   - Restrict SQL Database access to specific IPs
   - Use private endpoints for Key Vault

2. **Authentication**:
   - Use Managed Identity for all Azure service connections
   - Regularly rotate secrets in Key Vault
   - Enable Azure AD authentication for SQL Database

3. **Monitoring**:
   - Enable diagnostic logs for all services
   - Set up security alerts in Azure Security Center
   - Regular security assessments

## Cost Optimization

1. **Right-sizing**:
   - Start with Basic tier App Service Plan
   - Use Basic tier SQL Database for development
   - Scale up based on usage patterns

2. **Resource Management**:
   - Use Azure DevTest Labs for development environments
   - Set up auto-shutdown for non-production resources
   - Monitor costs with Azure Cost Management

This deployment guide provides a complete production-ready setup for PhotoSync on Azure with proper security, monitoring, and scalability considerations.
