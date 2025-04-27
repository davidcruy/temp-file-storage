using Microsoft.AspNetCore.Builder;

namespace TempFileStorage;

public interface ITempFileStorageBuilder
{
    IApplicationBuilder Builder { get; }
    TempFileStorageOptions Options { get; }
}

internal class TempFileStorageBuilder(IApplicationBuilder builder, TempFileStorageOptions options) : ITempFileStorageBuilder
{
    public IApplicationBuilder Builder { get; } = builder;
    public TempFileStorageOptions Options { get; } = options;
}