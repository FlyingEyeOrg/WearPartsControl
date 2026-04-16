# WearPartsControl

## 架构分层

### Domain

- 位置：`src/WearPartsControl/Domain`
- 职责：表达业务概念、规则和契约，不依赖数据库、网络、文件或 EF Core。
- 关键内容：
	- 实体：`BasicConfigurationEntity`、`WearPartDefinitionEntity`
	- 值对象：`ResourceNumber`、`PlcEndpoint`
	- 领域服务：`WearPartDefinitionDomainService`
	- 领域事件契约：`IDomainEvent`、`IDomainEventHandler<T>`
	- 仓储接口：`IRepository<TEntity, TId>`、`IBasicConfigurationRepository`、`IWearPartRepository`
		- `IRepository<TEntity, TId>` 已统一提供 `SoftDeleteAsync` 与事务能力（`BeginTransactionAsync`、`CommitTransactionAsync`、`RollbackTransactionAsync`、`SaveChangesAsync`），应用层仅依赖仓储接口即可完成 UoW 流程。
	- 领域异常与验证：`DomainBusinessException`、`DomainValidationException`、`DomainValidationRules`

### Infrastructure（EF Core）

- 位置：`src/WearPartsControl/Infrastructure`
- 职责：实现所有 I/O 与持久化细节，依赖 Domain 契约并提供具体实现。
- 关键内容：
	- `WearPartsControlDbContext`
	- EF Core 映射配置：
		- `EntityFrameworkCore/Configurations/BasicConfigurationEntityConfiguration`
		- `EntityFrameworkCore/Configurations/WearPartDefinitionEntityConfiguration`
	- 仓储实现：
		- `EntityFrameworkCore/Repositories/BasicConfigurationRepository`
		- `EntityFrameworkCore/Repositories/WearPartRepository`
	- 初始化与脚本：
		- `EntityFrameworkCore/SqliteDatabaseInitializer`
		- `EntityFrameworkCore/Migrations/001_initial_wearparts_schema.sql`

## 依赖方向

- `Domain` 不依赖 `Infrastructure`
- `Infrastructure` 可以依赖 `Domain`
- `ApplicationServices` 通过接口（仓储契约）访问领域数据

## 测试

- `tests/WearPartsControl.Tests/DomainValueObjectsTests.cs`
- `tests/WearPartsControl.Tests/WearPartDefinitionDomainServiceTests.cs`
- `tests/WearPartsControl.Tests/WearPartRepositoryTests.cs`