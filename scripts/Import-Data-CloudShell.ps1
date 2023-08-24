$blobUri = "https://cosmosdbcosmicworks.blob.core.windows.net/cosmic-works-small/product.json"
$result = Invoke-WebRequest -Uri $blobUri
$products = $result.Content | ConvertFrom-Json
Write-Output "Imported $($products.Length) products"

$blobUri = "https://cosmosdbcosmicworks.blob.core.windows.net/cosmic-works-small/customer.json"
$result = Invoke-WebRequest -Uri $blobUri
# The customers file has a BOM which needs to be ignored
$customers = $result.Content.Substring(1, $result.Content.Length - 1) | ConvertFrom-Json
Write-Output "Imported $($customers.Length) customers"

$module = Get-InstalledModule -Name 'CosmosDB'
if($module -ne $null)
{
    write-host "Module CosmosDB is avaiable"
}
else
{
    write-host "Module CosmosDB is not avaiable, installing..."
    Install-Module -Name CosmosDB -AllowClobber -force
}

Import-Module CosmosDB
$cosmosDbAccount = "edq44f5h5wh2k-cosmos-nosql"
$database = "database"
$resourceGroup = "ms-cosmosdb-openai-01"

Connect-AzAccount
Set-AzContext -SubscriptionName "Solliance MPN 12K"
$cosmosDbContext = New-CosmosDbContext -Account $cosmosDbAccount -Database $database -ResourceGroup $resourceGroup

foreach($product in $products)
{
    New-CosmosDbDocument -Context $cosmosDbContext -CollectionId 'product' -DocumentBody ($product | ConvertTo-Json) -PartitionKey $product.categoryId
}

foreach($customer in $customers)
{
    New-CosmosDbDocument -Context $cosmosDbContext -CollectionId 'customer' -DocumentBody ($customer | ConvertTo-Json) -PartitionKey $customer.customerId
}