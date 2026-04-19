# NLog.WCF
NLog [WCF target](https://github.com/NLog/NLog/wiki/LogReceiverService-target) for sending LogEvents to NLog Receiver Service (using WCF or Web Services)

[![Version](https://badge.fury.io/nu/NLog.WCF.svg)](https://www.nuget.org/packages/NLog.WCF)
[![AppVeyor](https://img.shields.io/appveyor/ci/NLog/NLog-WCF/master.svg)](https://ci.appveyor.com/project/NLog/NLog-WCF/branch/master)

### How to install

1) Install the package

    `Install-Package NLog.WCF` or in your csproj:

    ```xml
    <PackageReference Include="NLog.WCF" Version="6.*" />
    ```

2) Add to your nlog.config:

    ```xml
    <extensions>
        <add assembly="NLog.WCF"/>
    </extensions>
    ```

    Alternative register from code using [fluent configuration API](https://github.com/NLog/NLog/wiki/Fluent-Configuration-API):

    ```xml
    LogManager.Setup().SetupExtensions(ext => {
       ext.RegisterTarget<NLog.Targets.LogReceiverWebServiceTarget>();
    });
    ```

See also [NLog Wiki](https://github.com/NLog/NLog/wiki/LogReceiverService-target) for available options and examples.
