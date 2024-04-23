# .NET 8 Azure Blob Storage Library

This .NET 8 class library provides a simplified interface for interacting with Azure Blob Storage. It includes asynchronous methods to handle common blob storage operations such as uploading, downloading, deleting, and listing blobs within a container.

## Features

- Upload files to Azure Blob Storage
- Download files from Azure Blob Storage
- Delete blobs from Azure Blob Storage
- List all blobs in a specific container

## Getting Started

### Prerequisites

Before you begin, ensure you have the following:
- .NET 8 SDK installed
- Azure subscription and Azure Storage account
- An IDE that supports .NET development, such as Visual Studio or Visual Studio Code

### Installation

1. Clone this repository to your machine:

    ```bash
    git clone https://github.com/ssinanylmz/Library.Azure.Blob.git
    ```

2. Add the library to your .NET project:

    ```bash
    dotnet add reference /path/to/your/Library.Azure.Blob.csproj
    ```

3. Restore the project dependencies:

    ```bash
    dotnet restore
    ```

### Configuration

Add your Azure Blob Storage connection settings to your `appsettings.json`:

```json
{
    "AzureBlobSettings": {
        "ConnectionString": "your_connection_string",
        "ContainerName": "your_default_container_name"
    }
}
```
## Registering Services
In your Startup.cs or wherever you configure services, add the blob service to the dependency injection container:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.RegisterBlobService(Configuration);
}
```
