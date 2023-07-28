# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- Renamed `AxonServerConnectionFactoryDefaults` to `AxonServerConnectorDefaults` to allow it to be shared between `AxonServerConnection` and `AxonServerConnectionFactory`
- Renamed `AxonServerConnectionFactoryConfiguration` to `AxonServerConnectorConfiguration`  to allow it to be shared between `AxonServerConnection` and `AxonServerConnectionFactory`
- Renamed `AxonServerConnectionHeaders` to `AxonServerConnectorHeaders` to allow it to be shared between `AxonServerConnection` and `AxonServerConnectionFactory`
- Renamed `IAxonServerConnectionFactoryOptionsBuilder` to `IAxonServerConnectorOptionsBuilder` to allow it to be shared between `AxonServerConnection` and `AxonServerConnectionFactory`
- Renamed `AxonServerConnectionFactoryOptions` to `AxonServerConnectorOptions` to allow it to be shared between `AxonServerConnection` and `AxonServerConnectionFactory`
- Introduction of `IOwnerAxonServerConnection` to allow sharing behavior between a `SharedAxonServerConnection` and an `AxonServerConnection` 
- Renamed internal `AxonServerConnection` to `SharedAxonServerConnection` to make room for a public `AxonServerConnection`
- Introduction of `AxonServerConnection` as a stand alone connection that takes a `Context` and configuration options. This eases registration in the inversion of control container and auto connects whenever a property of the connection is accessed, except for `ClientIdentity`, `Context`, `IsConnected`, `IsClosed`, `IsReady`. Ideal for those scenarios where the consuming code is only ever interacting with one `Context`. Extended `ServiceCollectionExtensions` with `AddAxonServerConnection` overloads similar to `AddAxonServerConnectionFactory` overloads.
- Public API generation and verification
- Initial version

[unreleased]: https://github.com/AxonIQ/axonserver-connector-dotnet/compare/ORIGIN...HEAD