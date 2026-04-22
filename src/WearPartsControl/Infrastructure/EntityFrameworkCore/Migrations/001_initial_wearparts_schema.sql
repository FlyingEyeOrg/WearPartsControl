CREATE TABLE IF NOT EXISTS basic_configurations (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    FactoryCode TEXT NOT NULL,
    AreaCode TEXT NOT NULL,
    ProcedureCode TEXT NOT NULL,
    EquipmentCode TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PlcProtocolType TEXT NOT NULL,
    PlcIpAddress TEXT NOT NULL,
    PlcPort INTEGER NOT NULL,
    ShutdownPointAddress TEXT NULL,
    SiemensRack INTEGER NOT NULL,
    SiemensSlot INTEGER NOT NULL,
    IsStringReverse INTEGER NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_basic_configurations_ResourceNumber
ON basic_configurations(ResourceNumber);

CREATE TABLE IF NOT EXISTS tool_changes (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    Name TEXT NOT NULL,
    Code TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_tool_changes_Name
ON tool_changes(Name);

CREATE UNIQUE INDEX IF NOT EXISTS IX_tool_changes_Code
ON tool_changes(Code);

CREATE TABLE IF NOT EXISTS wear_part_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    ResourceNumber TEXT NOT NULL,
    PartName TEXT NOT NULL,
    InputMode TEXT NOT NULL,
    CurrentValueAddress TEXT NOT NULL,
    CurrentValueDataType TEXT NOT NULL,
    WarningValueAddress TEXT NOT NULL,
    WarningValueDataType TEXT NOT NULL,
    ShutdownValueAddress TEXT NOT NULL,
    ShutdownValueDataType TEXT NOT NULL,
    IsShutdown INTEGER NOT NULL,
    CodeMinLength INTEGER NOT NULL,
    CodeMaxLength INTEGER NOT NULL,
    LifetimeType TEXT NOT NULL,
    ToolChangeId TEXT NULL,
    PlcZeroClearAddress TEXT NULL,
    BarcodeWriteAddress TEXT NOT NULL,
    CONSTRAINT FK_wear_part_definitions_basic_configurations_ClientAppConfigurationId
        FOREIGN KEY (ClientAppConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE,
    CONSTRAINT FK_wear_part_definitions_tool_changes_ToolChangeId
        FOREIGN KEY (ToolChangeId)
        REFERENCES tool_changes (Id)
        ON DELETE SET NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_wear_part_definitions_ClientAppConfigurationId_PartName
ON wear_part_definitions(ClientAppConfigurationId, PartName);

CREATE TABLE IF NOT EXISTS wear_part_replacement_records (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    IsDeleted INTEGER NOT NULL,
    DeletedAt TEXT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    WearPartDefinitionId TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    PartName TEXT NOT NULL,
    OldBarcode TEXT NULL,
    NewBarcode TEXT NOT NULL,
    CurrentValue TEXT NOT NULL,
    WarningValue TEXT NOT NULL,
    ShutdownValue TEXT NOT NULL,
    OperatorWorkNumber TEXT NOT NULL,
    OperatorUserName TEXT NOT NULL,
    ReplacementReason TEXT NOT NULL,
    ReplacementMessage TEXT NOT NULL,
    ReplacedAt TEXT NOT NULL,
    DataType TEXT NULL,
    DataValue TEXT NULL,
    CONSTRAINT FK_wear_part_replacement_records_basic_configurations_ClientAppConfigurationId
        FOREIGN KEY (ClientAppConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE,
    CONSTRAINT FK_wear_part_replacement_records_wear_part_definitions_WearPartDefinitionId
        FOREIGN KEY (WearPartDefinitionId)
        REFERENCES wear_part_definitions (Id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_wear_part_replacement_records_WearPartDefinitionId_ReplacedAt
ON wear_part_replacement_records(WearPartDefinitionId, ReplacedAt DESC);

CREATE INDEX IF NOT EXISTS IX_wear_part_replacement_records_WearPartDefinitionId_NewBarcode
ON wear_part_replacement_records(WearPartDefinitionId, NewBarcode);

CREATE TABLE IF NOT EXISTS exceed_limit_records (
    Id TEXT NOT NULL PRIMARY KEY,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    UpdatedBy TEXT NOT NULL,
    ClientAppConfigurationId TEXT NOT NULL,
    WearPartDefinitionId TEXT NOT NULL,
    PartName TEXT NOT NULL,
    CurrentValue REAL NOT NULL,
    WarningValue REAL NOT NULL,
    ShutdownValue REAL NOT NULL,
    Severity TEXT NOT NULL,
    OccurredAt TEXT NOT NULL,
    NotificationMessage TEXT NOT NULL,
    CONSTRAINT FK_exceed_limit_records_basic_configurations_ClientAppConfigurationId
        FOREIGN KEY (ClientAppConfigurationId)
        REFERENCES basic_configurations (Id)
        ON DELETE CASCADE,
    CONSTRAINT FK_exceed_limit_records_wear_part_definitions_WearPartDefinitionId
        FOREIGN KEY (WearPartDefinitionId)
        REFERENCES wear_part_definitions (Id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_exceed_limit_records_WearPartDefinitionId_Severity_OccurredAt
ON exceed_limit_records(WearPartDefinitionId, Severity, OccurredAt DESC);
