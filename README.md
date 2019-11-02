# Netfuser 

Netfuser is a framework for post-build processing of .NET assemblies.

Netfuser currently allows to:
* Merge multiple assemblies into one
* Embed assemblies as .NET resources
* Protect from reverse engineering:
	* Obfuscate metadata names (namespaces, types and type members)
	* Obfuscate control flow
	* Obfuscate strings
	* Provide your own algorithms to obfuscate strings, names, control flow
* Safely change metadata names
* Inject types, type members and IL code
* Generate PDB for the resulting assembly
* Develop plugins to perform other actions

Netfuser is created for .NET developers, and unlike the similar tools:
* It does not have a GUI or CLI (and no plans to add these)
* It requires C# coding skills and basic understanding of .NET 
* It is highly configurable, but all configuration is performed in the C# code. There are no config files
* It is event-driven: almost every action emits an event that may be observed to change behavior
* It consists of plugins and provides developer-friendly well-documented API to develop custom plugins
* It is pretty fast. No benchmarks to back this statement, just a personal experience.

## Getting Netfuser

Netfuser is [available on NuGet](https://www.nuget.org/packages/Netfuser/) or can be [built from source](https://github.com/dotnet-toolset/Netfuser)
 

## Examples

Refer to the [Example](https://github.com/dotnet-toolset/Netfuser/Netfuser.Example) folder


## Development

This is an early alpha software, with lots of bugs and missing features.
It has been tested on a very limited number of assemblies, so the chances it won't work in your particular case are high.
Feature requests, bug repoorts and pull requests are welcome.

## Acknowledgements

Netfuser relies on dnlib (https://github.com/0xd4d/dnlib) to read, change and write .NET assemblies.
Many thanks to https://github.com/0xd4d for his extensive knowledge and acceptance of my pull requests.
Some ideas and implementations were taken from https://github.com/yck1509/ConfuserEx, an excellent, but (sadly) discontinued tool.

## Copyrights

* `Netfuser` Copyright (c) 2019 dyarkovoy@gmail.com
* `dnlib` Copyright (C) 2012-2019 de4dot@gmail.com
* `ConfuserEx` Copyright (c) 2014 yck1509  


