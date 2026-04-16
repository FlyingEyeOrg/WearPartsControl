CREATE TABLE IF NOT EXISTS basic_configurations (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    IsDeleted INTEGER NOT NULL,
    DeletedAt TEXT NULL,
    Remark TEXT NULL,
    SiteCode TEXT NOT NULL,
    FactoryCode TEXT NOT NULL,
    AreaCode TEXT NOT NULL,
    ProcedureCode TEXT NOT NULL,
    EquipmentCode TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PlcProtocolType TEXT NOT NULL,
    PlcIpAddress TEXT NOT NULL,
    PlcPort INTEGER NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_basic_configurations_ResourceNumber
ON basic_configurations(ResourceNumber);

CREATE TABLE IF NOT EXISTS wear_part_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    IsDeleted INTEGER NOT NULL,
    DeletedAt TEXT NULL,
    Remark TEXT NULL,
    BasicConfigurationId TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PartName TEXT NOT NULL,
    CurrentValueAddress TEXT NOT NULL,
    WarningValueAddress TEXT NOT NULL,
    ShutdownValueAddress TEXT NOT NULL,
    CONSTRAINT FK_wear_part_definitions_basic_configurations_BasicConfigurationId
        FOREIGN KEY (BasicConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_wear_part_definitions_BasicConfigurationId_PartName
ON wear_part_definitions(BasicConfigurationId, PartName);
