# OwlCore.ComponentModel [![Version](https://img.shields.io/nuget/v/OwlCore.ComponentModel.svg)](https://www.nuget.org/packages/OwlCore.ComponentModel)

Provides classes that are used to implement the run-time behavior of components.

## Featuring:
- **IAsyncInit** - A common interface used for asynchronous class initialization.
- **ISerializer** and **IAsyncSerializer** - An interface for serializing to and from a type.
- **IDelegatable{T}** - Indicates that the class is holding a reference to an implementation of T, which properties, events or methods may be delegated to.
- **ChainedProxyBuilder** - Builds a list of IDelegatable{T} into a proxied chain, where each item might delegate member access to the next item.
- **SettingsBase** - A base class for managing settings values as properties. Fast access in memory, with data persistence via OwlCore.Storage.

## Install
Published releases are available on [NuGet](https://www.nuget.org/packages/OwlCore.ComponentModel). To install, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console).

    PM> Install-Package OwlCore.ComponentModel
    
Or using [dotnet](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet)

    > dotnet add package OwlCore.ComponentModel

## Examples

### SettingsBase

This example uses source generators from System.Text.Json for serialization.

```cs
// Basic usage
var settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var settings = new MySettings(new SystemFolder(settingsPath));

await settings.LoadAsync();

if (settings.HasSeenOOBE)
    settings.Interval = TimeSpan.FromSeconds(0);

await settings.SaveAsync();

// Implementation
public class MySettings : SettingsBase
{
    public MySettings(IModifiableFolder settingsFolder)
        : base(settingsFolder, FilesCoreSettingsSerializer.Singleton)
    {
    }
    
    public bool HasSeenOOBE
    {
        get => GetSetting(() => false);
        set => SetSetting(value);
    }
    
    public TimeSpan Interval
    {
        get => GetSetting(() => TimeSpan.FromSeconds(5));
        set => SetSetting(value);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(TimeSpan))]
internal partial class MySettingsSerializerContext : JsonSerializerContext
{
}

public class MySettingsSerializer : IAsyncSerializer<Stream>
{
    public static MySettingsSerializer Singleton { get; } = new();
    
    /// <inheritdoc />
    public async Task<Stream> SerializeAsync<T>(T data, CancellationToken? cancellationToken = null)
    {
        var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, data, typeof(T), context: MySettingsSerializerContext.Default, cancellationToken: cancellationToken ?? CancellationToken.None);
        return stream;
    }

    /// <inheritdoc />
    public async Task<Stream> SerializeAsync(Type inputType, object data, CancellationToken? cancellationToken = null)
    {
        var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, data, inputType, context: MySettingsSerializerContext.Default, cancellationToken: cancellationToken ?? CancellationToken.None);
        return stream;
    }

    /// <inheritdoc />
    public async Task<TResult> DeserializeAsync<TResult>(Stream serialized, CancellationToken? cancellationToken = null)
    {
        var result = await JsonSerializer.DeserializeAsync(serialized, typeof(TResult), MySettingsSerializerContext.Default);
        Guard.IsNotNull(result);
        return (TResult)result;
    }

    /// <inheritdoc />
    public async Task<object> DeserializeAsync(Type returnType, Stream serialized, CancellationToken? cancellationToken = null)
    {
        var result = await JsonSerializer.DeserializeAsync(serialized, returnType, MySettingsSerializerContext.Default);
        Guard.IsNotNull(result);
        return result;
    }
}
```

## Financing

We accept donations, and we do not have any active bug bounties.

If you???re looking to contract a new project, new features, improvements or bug fixes, please contact me. 

## Versioning

Version numbering follows the Semantic versioning approach. However, if the major version is `0`, the code is considered alpha and breaking changes may occur as a minor update.

## License

We???re using the MIT license for 3 reasons:
1. We're here to share useful code. You may use any part of it freely, as the MIT license allows. 
2. A library is no place for viral licensing.
3. Easy code transition to larger community-based projects, such as the .NET Community Toolkit.

