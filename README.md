# OwlCore.ComponentModel [![Version](https://img.shields.io/nuget/v/OwlCore.ComponentModel.svg)](https://www.nuget.org/packages/OwlCore.ComponentModel)

Provides classes that are used to implement the run-time behavior of components.

## Featuring:
- **IAsyncInit** - A common interface used for asynchronous class initialization.
- **ISerializer** and **IAsyncSerializer** - An interface for serializing to and from a type.
- **IDelegable{T}** - Indicates that the class is holding a reference to an implementation of T, which properties, events or methods may be delegated to when accessing members.
- **ChainedProxyBuilder** - Builds a list of IDelegatable{T} into a proxied chain, where each item might delegate member access to the next item.

**Note**: SettingsBase has been moved to [OwlCore.ComponentModel.Settings](https://github.com/Arlodotexe/OwlCore.ComponentModel.Settings).

## Install
Published releases are available on [NuGet](https://www.nuget.org/packages/OwlCore.ComponentModel). To install, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console).

    PM> Install-Package OwlCore.ComponentModel
    
Or using [dotnet](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet)

    > dotnet add package OwlCore.ComponentModel

## Financing

We accept donations [here](https://github.com/sponsors/Arlodotexe) and [here](https://www.patreon.com/arlodotexe), and we do not have any active bug bounties.

## Versioning

Version numbering follows the Semantic versioning approach. However, if the major version is `0`, the code is considered alpha and breaking changes may occur as a minor update.

## License

All OwlCore code is licensed under the MIT License. OwlCore is licensed under the MIT License. See the [LICENSE](./src/LICENSE.txt) file for more details.
