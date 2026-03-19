namespace SupportAssignmentSystem.Core.Configuration;

public class StorageConfiguration
{
    public StorageType StorageType { get; set; } = StorageType.InMemory;
    public RedisConfiguration RedisConfiguration { get; set; } = new();
    public DatabaseConfiguration DatabaseConfiguration { get; set; } = new();
}

public enum StorageType
{
    InMemory,
    Redis,
    Database
}

public class RedisConfiguration
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "SupportAssignmentSystem:";
}

public class DatabaseConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Provider { get; set; } = "SqlServer"; // SqlServer, PostgreSQL
}
