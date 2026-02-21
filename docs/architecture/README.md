# Architecture Documentation

This folder contains comprehensive architecture documentation for the D3dxSkinManager project.

## Primary Reference

**[CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md)** - ⭐⭐⭐ **START HERE**

Complete guide covering:
- System architecture overview
- Module structure (backend & frontend)
- IPC message format and routing
- Service patterns and dependency injection
- Data flow examples
- Guide for adding new features

## Detailed Architecture Documents

### Core Architecture
- **[MODULE_STRUCTURE.md](MODULE_STRUCTURE.md)** - Module organization guidelines
- **[MODULE_QUICK_REFERENCE.md](MODULE_QUICK_REFERENCE.md)** - Quick reference for module structure
- **[DOMAIN_DESIGN.md](DOMAIN_DESIGN.md)** - ⭐ Domain boundaries & service responsibilities

### Backend Architecture
- **[APP_FACADE_REFACTORING.md](APP_FACADE_REFACTORING.md)** - ⭐ AppFacade top-level router (current)
- **[FACADE_REFACTORING.md](FACADE_REFACTORING.md)** - Module facade split pattern (historical)
- **[SERVICE_REGISTRATION_ARCHITECTURE.md](SERVICE_REGISTRATION_ARCHITECTURE.md)** - Dependency injection setup
- **[MIGRATION_ARCHITECTURE.md](MIGRATION_ARCHITECTURE.md)** - ⭐ Migration system (step-based workflow)
- **[MIGRATION_PARSER_ARCHITECTURE.md](MIGRATION_PARSER_ARCHITECTURE.md)** - Parser service details
- **[PATH_CONVENTIONS.md](PATH_CONVENTIONS.md)** - Path handling patterns (relative paths)

### Frontend Architecture
- **[FRONTEND_CONTEXT_ARCHITECTURE.md](FRONTEND_CONTEXT_ARCHITECTURE.md)** - ⭐ React context system
- **[FRONTEND_SERVICE_ARCHITECTURE.md](FRONTEND_SERVICE_ARCHITECTURE.md)** - Frontend service pattern (BaseModuleService)
- **[FINAL_MODULE_STRUCTURE.md](FINAL_MODULE_STRUCTURE.md)** - Final module structure decisions

## Quick Navigation

| Need to... | Read |
|------------|------|
| Understand the system | [CURRENT_ARCHITECTURE.md](CURRENT_ARCHITECTURE.md) ⭐⭐⭐ |
| Understand domain boundaries | [DOMAIN_DESIGN.md](DOMAIN_DESIGN.md) ⭐⭐⭐ |
| Add a new module | [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md) |
| Understand IPC routing | [APP_FACADE_REFACTORING.md](APP_FACADE_REFACTORING.md) |
| Create React context | [FRONTEND_CONTEXT_ARCHITECTURE.md](FRONTEND_CONTEXT_ARCHITECTURE.md) |
| Create a frontend service | [FRONTEND_SERVICE_ARCHITECTURE.md](FRONTEND_SERVICE_ARCHITECTURE.md) |
| Set up dependency injection | [SERVICE_REGISTRATION_ARCHITECTURE.md](SERVICE_REGISTRATION_ARCHITECTURE.md) |
| Handle file paths | [PATH_CONVENTIONS.md](PATH_CONVENTIONS.md) |
| Understand migration system | [MIGRATION_ARCHITECTURE.md](MIGRATION_ARCHITECTURE.md) |

## Related Documentation

- **[AI_GUIDE.md](../AI_GUIDE.md)** - AI assistant guide with navigation
- **[KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)** - Component/service location index
- **[docs/core/](../core/)** - Project fundamentals
- **[docs/features/](../features/)** - Feature documentation

---

**For historical documentation**, see [docs/archive/](../archive/)
