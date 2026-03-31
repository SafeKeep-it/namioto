# CodeAnalysis — MAP

## Purpose

- Roslyn source generator library providing code generation for Minimal API endpoint metadata, HTTP client implementation, SpoolBus handler/client generation, and symbol metadata extraction. Generates `.generated.cs` files with STJ serialization contexts.
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Web/WebApplicationMapGenerator.cs:10-11

## Key directories

- Web/: Minimal API endpoint detection and JSON context generation
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Web/WebApplicationMapGenerator.cs:1
- SpoolBus/: Message-based handler/client generator
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/SpoolBus/SpoolBusHandlerGenerator.cs:1
- Http/: HTTP client interface implementation generator
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Http/HttpClientGenerator.cs:1
- Common/: Shared infrastructure (manifest, symbol extraction, JSON emitter)
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/GeneratorManifest.cs:1
- Common/Json/: STJ serialization context generation helpers
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/Json/JsonSerializerContextEmitter.cs:1

## Primary entry points

- Web/WebApplicationMapGenerator.cs: Detects Minimal API endpoints (MapGet/MapPost/etc.), generates .Web.generated.cs files with STJ attributes + runtime code
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Web/WebApplicationMapGenerator.cs:10-37
- SpoolBus/SpoolBusHandlerGenerator.cs: Generates server-side handlers and client implementations for message-based communication, creates .SpoolBus.generated.cs files
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/SpoolBus/SpoolBusHandlerGenerator.cs:10-40
- Http/HttpClientGenerator.cs: Generates HTTP client implementations from interface definitions, supports streaming via IAsyncEnumerable<T>, creates .Http.generated.cs files
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Http/HttpClientGenerator.cs:10-81
- Common/MethodCallGraphGenerator.cs: Extracts symbol metadata (methods, properties, types), tracks dependencies and synthesized members, writes to .ontology/docs/.tmp/*.shape.ndjson
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/MethodCallGraphGenerator.cs:9-46
- Common/GeneratorManifest.cs: Tracks generated file mappings for incremental builds, handles rename detection via obj/generators/<generator>/manifest.txt
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/GeneratorManifest.cs:8

## Public API surface

- Comptatata.CodeAnalysis.Web.WebApplicationMapGenerator: Source generator for Minimal API endpoint JSON contexts
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Web/WebApplicationMapGenerator.cs:10
- Comptatata.CodeAnalysis.SpoolBus.SpoolBusHandlerGenerator: Source generator for message-based handlers/clients
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/SpoolBus/SpoolBusHandlerGenerator.cs:10
- Comptatata.CodeAnalysis.Http.HttpClientGenerator: Source generator for HTTP client implementations
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Http/HttpClientGenerator.cs:10
- Comptatata.CodeAnalysis.Common.MethodCallGraphGenerator: Source generator for symbol metadata extraction
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/MethodCallGraphGenerator.cs:9
- Comptatata.CodeAnalysis.Common.GeneratorManifest: Manifest tracking class for incremental builds
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/GeneratorManifest.cs:8
- Comptatata.CodeAnalysis.Common.JsonSerializerContextEmitter: Helper for generating STJ serialization contexts with polymorphic discriminators and token chains
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/Json/JsonSerializerContextEmitter.cs:6

## Constraints

- Target framework: netstandard2.0
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/CodeAnalysis.csproj:4
- IsRoslynComponent: true (requires netstandard2.0 compatibility)
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/CodeAnalysis.csproj:5
- Uses Microsoft.CodeAnalysis.CSharp 5.0.0 and Microsoft.CodeAnalysis.Analyzers 3.11.0 as private assets
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/CodeAnalysis.csproj:13-14

## Common tasks

- Add a new source generator: Create class implementing IIncrementalGenerator with [Generator(LanguageNames.CSharp)], define registration syntax detection and source output
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Web/WebApplicationMapGenerator.cs:10
- Generate STJ serialization contexts: Use JsonSerializerContextEmitter.EmitContext or EmitRuntimeMembers with SerializationGraph populated via AddSerializableTypes
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/Json/JsonSerializerContextEmitter.cs:644
- Track generated files for incremental builds: Use GeneratorManifest.LoadOrCreate in Execute(), record Generation per source file, cleanup stale entries
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/GeneratorManifest.cs:20

## Notes

- WebApplicationMapGenerator writes attributes-only to disk (.Web.generated.cs) and runtime code via AddSource for IDE updates
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Web/WebApplicationMapGenerator.cs:369-389
- SpoolBusHandlerGenerator handles both one-way (void) and two-way messages with async/sync dispatchers
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/SpoolBus/SpoolBusHandlerGenerator.cs:680-717
- HttpClientGenerator supports streaming via IAsyncEnumerable<T> with NDJSON and standard JSON deserialization
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Http/HttpClientGenerator.cs:468-495
- MethodCallGraphGenerator detects synthesized members (records, compiler-generated) and writes shape files to .ontology/docs/.tmp/<project>.<timestamp>.shape.ndjson
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/MethodCallGraphGenerator.cs:105-160
- BalorClient sends lightweight fire-and-forget touch notifications to Balor.Daemon via named pipe with channel-based producer/consumer
  Evidence: /Users/serialseb/Dev/comptatata/src/dotnet/CodeAnalysis/Common/BalorClient.cs:11
