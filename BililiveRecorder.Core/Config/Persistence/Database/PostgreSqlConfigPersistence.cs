#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BililiveRecorder.Core.Api;
using BililiveRecorder.Core.Config.Persistence.Database.Entities;
using BililiveRecorder.Core.Config.V3;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core.Config.Persistence.Database
{
    /// <summary>
    /// PostgreSQL-based configuration persistence using hybrid schema
    /// (Core columns + JSONB config_overrides)
    /// </summary>
    public class PostgreSqlConfigPersistence : IConfigPersistence
    {
        private readonly string connectionString;
        private readonly ILogger logger;
        private readonly JsonSerializerSettings jsonSettings;
        private readonly bool useHybridSchema;

        public PostgreSqlConfigPersistence(string connectionString, ILogger? logger = null, bool useHybridSchema = true)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.logger = logger?.ForContext<PostgreSqlConfigPersistence>() ?? Log.ForContext<PostgreSqlConfigPersistence>();
            this.useHybridSchema = useHybridSchema;

            this.jsonSettings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };

            var schemaType = useHybridSchema ? "hybrid schema" : "JSON blob";
            this.logger.Information($"PostgreSQL config persistence initialized with {schemaType}");
        }

        public ConfigV3? Load()
        {
            if (this.useHybridSchema)
            {
                return LoadFromHybridSchema();
            }
            else
            {
                return LoadFromJsonBlob();
            }
        }

        private ConfigV3? LoadFromHybridSchema()
        {
            try
            {
                using var context = CreateDbContext();

                // Load global config from key-value table
                var globalConfigEntities = context.GlobalConfigs.ToList();
                var globalConfig = new GlobalConfig();

                if (globalConfigEntities.Any())
                {
                    this.logger.Information("[PostgreSQL] Loading {Count} global config entries from hybrid schema", globalConfigEntities.Count);
                    globalConfig = ConvertGlobalConfigEntitiesToConfig(globalConfigEntities);
                }
                else
                {
                    this.logger.Information("[PostgreSQL] No global config found, using defaults");
                }

                // Load rooms from rooms table
                var roomEntities = context.Rooms.OrderBy(r => r.Id).ToList();
                var rooms = new List<RoomConfig>();

                if (roomEntities.Any())
                {
                    this.logger.Information("[PostgreSQL] Loading {Count} rooms from hybrid schema", roomEntities.Count);
                    rooms = roomEntities.Select(e => ConvertRoomEntityToConfig(e, globalConfig)).ToList();
                }
                else
                {
                    this.logger.Information("[PostgreSQL] No rooms found in database");
                }

                var config = new ConfigV3
                {
                    Global = globalConfig,
                    Rooms = rooms
                };

                this.logger.Information("[PostgreSQL] Config loaded successfully from hybrid schema: {RoomCount} rooms", config.Rooms.Count);
                return config;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[PostgreSQL] Failed to load config from hybrid schema");
                return null;
            }
        }

        private ConfigV3? LoadFromJsonBlob()
        {
            try
            {
                using var context = CreateDbContext();
                var entity = context.Configs.FirstOrDefault(c => c.Id == 1);

                if (entity == null)
                {
                    this.logger.Information("[PostgreSQL] No config found in database, initializing default config");
                    return new ConfigV3();
                }

                this.logger.Information("[PostgreSQL] Loading config from database, version: {Version}", entity.Version);

                // Deserialize global config
                var globalConfig = JsonConvert.DeserializeObject<GlobalConfig>(entity.GlobalConfigJson, this.jsonSettings);
                if (globalConfig == null)
                {
                    this.logger.Warning("Failed to deserialize global config, using default");
                    globalConfig = new GlobalConfig();
                }

                // Deserialize rooms config
                var roomsConfig = JsonConvert.DeserializeObject<List<RoomConfig>>(entity.RoomsConfigJson, this.jsonSettings);
                if (roomsConfig == null)
                {
                    this.logger.Warning("Failed to deserialize rooms config, using empty list");
                    roomsConfig = new List<RoomConfig>();
                }

                var config = new ConfigV3
                {
                    Global = globalConfig,
                    Rooms = roomsConfig
                };

                this.logger.Information("[PostgreSQL] Config loaded successfully from database: {RoomCount} rooms", config.Rooms.Count);
                return config;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[PostgreSQL] Failed to load config from database");
                return null;
            }
        }

        public bool Save(ConfigV3 config)
        {
            if (this.useHybridSchema)
            {
                return SaveToHybridSchema(config);
            }
            else
            {
                return SaveToJsonBlob(config);
            }
        }

        private bool SaveToHybridSchema(ConfigV3 config)
        {
            try
            {
                if (config.DisableConfigSave)
                {
                    this.logger.Debug("[PostgreSQL] Skipping save config because DisableConfigSave is true");
                    return true;
                }

                using var context = CreateDbContext();

                // Save global config to key-value table
                SaveGlobalConfigToEntities(context, config.Global);

                // Save rooms to rooms table
                SaveRoomsToEntities(context, config.Rooms);

                context.SaveChanges();
                this.logger.Information("[PostgreSQL] Config saved to hybrid schema successfully ({RoomCount} rooms)", config.Rooms.Count);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[PostgreSQL] Failed to save config to hybrid schema");
                return false;
            }
        }

        private bool SaveToJsonBlob(ConfigV3 config)
        {
            try
            {
                if (config.DisableConfigSave)
                {
                    this.logger.Debug("[PostgreSQL] Skipping save config because DisableConfigSave is true");
                    return true;
                }

                using var context = CreateDbContext();

                // Serialize global and rooms config
                var globalJson = JsonConvert.SerializeObject(config.Global, Formatting.None, this.jsonSettings);
                var roomsJson = JsonConvert.SerializeObject(config.Rooms, Formatting.None, this.jsonSettings);

                var entity = context.Configs.FirstOrDefault(c => c.Id == 1);
                if (entity == null)
                {
                    // Create new config entity
                    entity = new ConfigEntity
                    {
                        Id = 1,
                        Version = config.Version,
                        GlobalConfigJson = globalJson,
                        RoomsConfigJson = roomsJson,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Configs.Add(entity);
                    this.logger.Information("[PostgreSQL] Creating new config entry in database");
                }
                else
                {
                    // Update existing config
                    entity.Version = config.Version;
                    entity.GlobalConfigJson = globalJson;
                    entity.RoomsConfigJson = roomsJson;
                    entity.UpdatedAt = DateTime.UtcNow;
                    this.logger.Debug("[PostgreSQL] Updating existing config in database");
                }

                context.SaveChanges();
                this.logger.Information("[PostgreSQL] Config saved to database successfully ({RoomCount} rooms)", config.Rooms.Count);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[PostgreSQL] Failed to save config to database");
                return false;
            }
        }

        public bool IsAvailable()
        {
            try
            {
                using var context = CreateDbContext();
                var canConnect = context.Database.CanConnect();
                if (canConnect)
                {
                    this.logger.Debug("[PostgreSQL] Database connection check successful");
                }
                else
                {
                    this.logger.Warning("[PostgreSQL] Database connection check failed");
                }
                return canConnect;
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "[PostgreSQL] Database connection check failed");
                return false;
            }
        }

        public bool Initialize()
        {
            try
            {
                this.logger.Information("[PostgreSQL] Initializing PostgreSQL database for config persistence");

                using var context = CreateDbContext();

                // Create database and tables if they don't exist
                context.Database.Migrate();

                this.logger.Information("[PostgreSQL] Database initialized successfully, config persistence ready");
                return true;
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "[PostgreSQL] Failed to initialize PostgreSQL database");
                return false;
            }
        }

        #region Hybrid Schema Conversion Methods

        private GlobalConfig ConvertGlobalConfigEntitiesToConfig(List<GlobalConfigEntity> entities)
        {
            var globalConfig = new GlobalConfig();
            var configType = typeof(GlobalConfig);

            foreach (var entity in entities)
            {
                var property = configType.GetProperty(entity.Key, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    this.logger.Warning("[PostgreSQL] Unknown global config key: {Key}", entity.Key);
                    continue;
                }

                try
                {
                    var propertyType = property.PropertyType;

                    // Check if this is an Optional<T> property (properties starting with "Optional")
                    if (propertyType.IsGenericType &&
                        propertyType.GetGenericTypeDefinition().Name.StartsWith("Optional"))
                    {
                        // Get the inner type T from Optional<T>
                        var innerType = propertyType.GetGenericArguments()[0];

                        // Parse the value to the inner type
                        object? innerValue = entity.ValueType switch
                        {
                            "string" => entity.Value,
                            "int" => int.Parse(entity.Value),
                            "uint" => uint.Parse(entity.Value),
                            "bool" => bool.Parse(entity.Value),
                            "double" => double.Parse(entity.Value),
                            "enum" => Enum.Parse(innerType, entity.Value),
                            _ => entity.Value
                        };

                        // Create an instance of Optional<T>
                        var optionalInstance = Activator.CreateInstance(propertyType);

                        // Set HasValue = true and Value = innerValue
                        var hasValueProp = propertyType.GetProperty("HasValue");
                        var valueProp = propertyType.GetProperty("Value");

                        if (hasValueProp != null && valueProp != null && optionalInstance != null)
                        {
                            hasValueProp.SetValue(optionalInstance, true);
                            valueProp.SetValue(optionalInstance, innerValue);
                            property.SetValue(globalConfig, optionalInstance);
                        }
                    }
                    else
                    {
                        // Non-Optional property, set directly
                        object? value = entity.ValueType switch
                        {
                            "string" => entity.Value,
                            "int" => int.Parse(entity.Value),
                            "uint" => uint.Parse(entity.Value),
                            "bool" => bool.Parse(entity.Value),
                            "double" => double.Parse(entity.Value),
                            "enum" => Enum.Parse(propertyType, entity.Value),
                            _ => entity.Value
                        };

                        property.SetValue(globalConfig, value);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "[PostgreSQL] Failed to convert global config key {Key} with value type {ValueType}", entity.Key, entity.ValueType);
                }
            }

            return globalConfig;
        }

        private RoomConfig ConvertRoomEntityToConfig(RoomEntity entity, GlobalConfig globalConfig)
        {
            var roomConfig = new RoomConfig
            {
                RoomUrl = entity.RoomUrl,
                Platform = entity.Platform,
                AutoRecord = entity.AutoRecord
            };

            // Parse config_overrides JSONB if present
            if (!string.IsNullOrEmpty(entity.ConfigOverrides))
            {
                try
                {
                    var overrides = JObject.Parse(entity.ConfigOverrides);
                    ApplyConfigOverrides(roomConfig, overrides);
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "[PostgreSQL] Failed to parse config overrides for room {RoomUrl}", entity.RoomUrl);
                }
            }

            // Set parent config to enable hierarchical property inheritance
            // This is critical for proper config value resolution
            roomConfig.SetParent(globalConfig);

            return roomConfig;
        }

        private void ApplyConfigOverrides(RoomConfig roomConfig, JObject overrides)
        {
            var configType = typeof(RoomConfig);

            foreach (var prop in overrides.Properties())
            {
                var property = configType.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    this.logger.Warning("[PostgreSQL] Unknown room config property: {Property}", prop.Name);
                    continue;
                }

                // Skip read-only properties (properties without setters)
                if (!property.CanWrite)
                {
                    this.logger.Debug("[PostgreSQL] Skipping read-only property: {Property}", prop.Name);
                    continue;
                }

                try
                {
                    var propertyType = property.PropertyType;

                    // Check if property is HierarchicalPropertyDefault<T>
                    if (propertyType.IsGenericType &&
                        propertyType.GetGenericTypeDefinition().Name.StartsWith("HierarchicalPropertyDefault"))
                    {
                        // Get the inner type T
                        var innerType = propertyType.GetGenericArguments()[0];

                        // Deserialize the value to the inner type
                        var innerValue = prop.Value.ToObject(innerType);

                        // Create HierarchicalPropertyDefault<T> instance
                        var wrapperInstance = Activator.CreateInstance(propertyType);

                        // Set HasValue = true and Value = innerValue
                        var hasValueProp = propertyType.GetProperty("HasValue");
                        var valueProp = propertyType.GetProperty("Value");

                        if (hasValueProp != null && valueProp != null)
                        {
                            hasValueProp.SetValue(wrapperInstance, true);
                            valueProp.SetValue(wrapperInstance, innerValue);
                            property.SetValue(roomConfig, wrapperInstance);
                        }
                    }
                    else
                    {
                        // Non-HierarchicalPropertyDefault property, deserialize directly
                        var value = prop.Value.ToObject(propertyType);
                        property.SetValue(roomConfig, value);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "[PostgreSQL] Failed to apply config override for property {Property}", prop.Name);
                }
            }
        }

        private void SaveGlobalConfigToEntities(BilibiliRecorderDbContext context, GlobalConfig globalConfig)
        {
            var existingEntities = context.GlobalConfigs.ToList();
            var configType = typeof(GlobalConfig);
            var properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Track valid property names (all properties that exist in the current GlobalConfig class)
            var validPropertyNames = new HashSet<string>(properties.Select(p => p.Name));

            foreach (var property in properties)
            {
                var value = property.GetValue(globalConfig);
                if (value == null)
                    continue;

                var key = property.Name;
                string stringValue;
                string valueType;

                // Check if this is an Optional<T> property
                if (property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition().Name.StartsWith("Optional"))
                {
                    // Get HasValue and Value properties from Optional<T>
                    var hasValueProp = property.PropertyType.GetProperty("HasValue");
                    var valueProp = property.PropertyType.GetProperty("Value");

                    if (hasValueProp == null || valueProp == null)
                        continue;

                    var hasValue = (bool?)hasValueProp.GetValue(value);
                    if (hasValue != true)
                        continue; // Skip Optional properties that don't have a value set

                    // Get the actual value from Optional<T>
                    var actualValue = valueProp.GetValue(value);
                    if (actualValue == null)
                        continue;

                    stringValue = actualValue.ToString() ?? string.Empty;

                    // Get the inner type T from Optional<T>
                    var innerType = property.PropertyType.GetGenericArguments()[0];
                    valueType = GetValueType(innerType);
                }
                else
                {
                    // Non-Optional property
                    stringValue = value.ToString() ?? string.Empty;
                    valueType = GetValueType(property.PropertyType);
                }

                var existingEntity = existingEntities.FirstOrDefault(e => e.Key == key);
                if (existingEntity != null)
                {
                    existingEntity.Value = stringValue;
                    existingEntity.ValueType = valueType;
                    existingEntity.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    context.GlobalConfigs.Add(new GlobalConfigEntity
                    {
                        Key = key,
                        Value = stringValue,
                        ValueType = valueType,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Clean up properties that no longer exist in GlobalConfig class
            var obsoleteEntities = existingEntities.Where(e => !validPropertyNames.Contains(e.Key)).ToList();
            if (obsoleteEntities.Any())
            {
                this.logger.Information("[PostgreSQL] Cleaning up {Count} obsolete global config properties from database: {Keys}",
                    obsoleteEntities.Count,
                    string.Join(", ", obsoleteEntities.Select(e => e.Key)));

                foreach (var obsoleteEntity in obsoleteEntities)
                {
                    context.GlobalConfigs.Remove(obsoleteEntity);
                }
            }
        }

        private void SaveRoomsToEntities(BilibiliRecorderDbContext context, List<RoomConfig> rooms)
        {
            var existingEntities = context.Rooms.ToList();

            // Update or add rooms
            foreach (var room in rooms)
            {
                var existingEntity = existingEntities.FirstOrDefault(e =>
                    e.RoomUrl == room.RoomUrl && e.Platform == room.Platform);

                if (existingEntity != null)
                {
                    // Update existing room
                    existingEntity.AutoRecord = room.AutoRecord;
                    existingEntity.ConfigOverrides = SerializeConfigOverrides(room);
                    existingEntity.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Add new room
                    context.Rooms.Add(new RoomEntity
                    {
                        RoomUrl = room.RoomUrl,
                        Platform = room.Platform,
                        AutoRecord = room.AutoRecord,
                        Enabled = true,
                        ConfigOverrides = SerializeConfigOverrides(room),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Remove rooms that no longer exist in config
            var roomsToRemove = existingEntities.Where(e =>
                !rooms.Any(r => r.RoomUrl == e.RoomUrl && r.Platform == e.Platform)).ToList();

            foreach (var roomToRemove in roomsToRemove)
            {
                context.Rooms.Remove(roomToRemove);
            }
        }

        private string? SerializeConfigOverrides(RoomConfig roomConfig)
        {
            var overrides = new JObject();
            var configType = typeof(RoomConfig);
            var properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Skip core properties that are already columns
                if (property.Name == "RoomUrl" || property.Name == "Platform" || property.Name == "AutoRecord")
                    continue;

                var value = property.GetValue(roomConfig);
                if (value != null)
                {
                    // Only serialize properties that have been explicitly set (HierarchicalPropertyDefault with HasValue=true)
                    var hasValueProperty = value.GetType().GetProperty("HasValue");
                    if (hasValueProperty != null)
                    {
                        var hasValue = (bool?)hasValueProperty.GetValue(value);
                        if (hasValue == true)
                        {
                            var valueProperty = value.GetType().GetProperty("Value");
                            var actualValue = valueProperty?.GetValue(value);
                            // Store the actual value, not the HierarchicalPropertyDefault wrapper
                            if (actualValue != null)
                            {
                                overrides[property.Name] = JToken.FromObject(actualValue);
                            }
                        }
                    }
                    else
                    {
                        // Non-HierarchicalPropertyDefault properties
                        overrides[property.Name] = JToken.FromObject(value);
                    }
                }
            }

            return overrides.Count > 0 ? overrides.ToString(Formatting.None) : null;
        }

        private string GetValueType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(double)) return "double";
            if (type.IsEnum) return "enum";
            return "string";
        }

        #endregion

        private BilibiliRecorderDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<BilibiliRecorderDbContext>();
            optionsBuilder.UseNpgsql(this.connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "recorder");
            });

            return new BilibiliRecorderDbContext(optionsBuilder.Options);
        }
    }
}
#endif
