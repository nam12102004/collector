---
name: create-dotnet-crud-feature
description: Strict workflow for adding a CRUD resource inside an established .NET modular-monolith module that uses Repository + Unit of Work and the documented API conventions. Use only when the modular-monolith activation gate passes and a canonical module feature confirms these patterns. For layered monoliths, vertical slices, or services, use create-dotnet-feature instead.
---

# Create .NET CRUD Feature

Apply `dotnet-backend-standards` and `modular-dotnet-standards` with this workflow.

## Activation Gate

Confirm all of the following before editing:

- the target is an established bounded-context module;
- module ownership and persistence boundaries are visible in code;
- a canonical CRUD feature confirms Repository + Unit of Work and API conventions;
- the requested resource belongs to this module.

If any condition fails, do not force this template. Use `create-dotnet-feature` and preserve the detected architecture.

## 0. Discover Local Patterns

Before adding files, inspect one existing feature in the same module. Match its CQRS vs service style, route style, response envelope, error code placement, migration process, and test conventions.

## 1. Entity And EF Configuration

Create the entity under `Domain/Entities/`:

```csharp
namespace {Company}.{ModuleName}.Domain.Entities;

public sealed class Product : BaseEntity<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = [];
}
```

Create EF configuration under `Infrastructure/Persistence/Configurations/`:

```csharp
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).IsRequired().HasMaxLength(256);
        builder.Property(product => product.Description).HasMaxLength(1024);
    }
}
```

Add `DbSet<Product>` if the module's DbContext declares sets explicitly.

## 2. Repository And Unit Of Work

- Use the generic repository for simple CRUD.
- Add a typed repository only for custom queries or domain-specific retrieval.
- Put repository interfaces in Domain and implementations in Infrastructure.
- Do not expose `IQueryable<T>`.
- Keep paging/filtering/sorting inside repository methods.

## 3. DTOs

Create request/response records under `Application/DTOs/` or the module's feature folder:

```csharp
public sealed record CreateProductRequest(string Name, string? Description);
public sealed record UpdateProductRequest(string Name, string? Description);
public sealed record ProductDto(Guid Id, string Name, string? Description, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
```

Use the target repo's ID type and audit field names.

## 4. Validation And Mapping

Add FluentValidation validators:

```csharp
public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(256);
    }
}
```

Use the approved AutoMapper 16.x baseline and add a profile outside the controller. For EF read models, prefer `ProjectTo<TDto>()` so projection stays in SQL. Preserve a different established mapper only when repository evidence proves it is the deliberate standard; ask before replacing it.

```csharp
public sealed class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>();
        CreateMap<CreateProductRequest, Product>();
        CreateMap<UpdateProductRequest, Product>();
    }
}
```

## 5. Service Or CQRS Handlers

Use the module's existing pattern.

Service example:

```csharp
public sealed class ProductService(
    IRepository<Product, Guid> productRepository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IProductService
{
    public async Task<ProductDto> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = mapper.Map<Product>(request);
        product.Id = Guid.NewGuid();
        product.CreatedAtUtc = DateTime.UtcNow;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await productRepository.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ProductDto>(product);
    }
}
```

Register service/interface, handlers, validators, mapper profiles, repositories, and Unit of Work in the module DI entrypoint if assembly scanning does not already handle them.

If the handler performs more than one write (updating the entity plus inserting a related record, or logging an audit entry), wrap the sequence in `unitOfWork.BeginTransactionAsync()`/`CommitTransactionAsync()` rather than calling `SaveChangesAsync` more than once independently. If it calls a shared side-effect helper (audit logging, notifications), confirm that helper stages changes instead of committing on its own — otherwise calling it from a bulk/loop caller silently turns one operation into many partial ones.

## 6. API Endpoints

Create a controller or Minimal API endpoint in the module. Default to authenticated, versioned, wrapped responses.

Controller example; the use-case service owns mapping and returns application data, while the API boundary owns the HTTP envelope:

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public sealed class ProductController(IProductService productService) : ControllerBase
{
    [HttpPost]
    [HasPermission(ProductPermissionConstant.Create)]
    public async Task<IActionResult> CreateProductAsync(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var data = await productService.CreateProductAsync(request, cancellationToken);
        return Ok(new SuccessResponse<ProductDto>(
            ProductSuccessCodeConstant.ProductCreated,
            ProductMessageConstant.Created,
            data));
    }
}
```

Every successful operation returns HTTP 200 with `SuccessResponse<T>(Code, Message, Data)`, including create and delete. Construct the envelope in the controller, or use a shared filter only when endpoint metadata explicitly supplies the success code and message. Add real success/error code constants. The backend message is a safe fallback; Angular localizes by code. Do not leave inline placeholder codes in client-facing responses.

## 7. Migration

Use repo migration scripts when present. Otherwise use the existing `dotnet ef migrations add` pattern for the owning module DbContext and startup project. Never hand-create migration, designer, or model-snapshot files. Inspect the generated `Up`/`Down` operations and snapshot, then run the repository drift check or `dotnet ef migrations has-pending-model-changes` when supported. Do not apply the migration merely as verification.

## 8. Tests

Add focused service/use-case tests for implemented CRUD behavior. Cover success behavior and derive failure cases from the exceptions, validation outcomes, and branches that the service logic actually contains.

Mock repositories and Unit of Work. Avoid EF in-memory for unit tests.

## 9. Verify

Build the solution. Do not run focused tests, start the application, open Swagger, or launch browser/runtime tooling unless explicitly requested. Inspect route discovery and endpoint wiring statically.

Review authorization/scoping, bounded list behavior, migration wiring, and atomic writes statically. Report the build result and hand the focused test/Swagger/runtime checks to the user.
