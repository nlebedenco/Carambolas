# Carambolas

Carambolas is an ever evolving general purpose support library comprised of multiple [**.NET Standard 2.0**](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard2.0.md)
assemblies. In particular, <ins>it features a custom multi-channel reliable UDP protocol implementation intended for low latency/low [bandwidth-delay product](#what-is-the-bandwidth-delay-product)
network applications with soft real-time constraints</ins>.

The first release is a minimal core with a fully functional network module. This repository is structured around a single solution because I plan to expand 
by adding new modules in the future.

Test coverage is still minimal so no level of correctness should be implied without a close inspection of the source code. This is an on-going project in its 
earliest stages. 

## Binaries

Binaries are soon to be available in the [archive section]((https://github.com/nlebedenco/Carambolas/releases)) or in the form of [nuget packages](https://www.nuget.org/packages/carambolas/). 

[![GitHub release](https://img.shields.io/github/release/nlebedenco/Carambolas.svg?style=flat-square)](https://github.com/nlebedenco/Carambolas/releases) 
[![NuGet package](https://img.shields.io/nuget/v/carambolas.svg)](https://www.nuget.org/packages/carambolas/)


## Quick Start


The **local host** is represented by an instance of *Carambolas.Net.Host*. It must be used to connect to a remote host or accept incoming connections. 

Every **remote host** is represented by an instance of *Carambolas.Net.Peer*. 

Events like connection, disconnection and data are received through the host object while peer objects may be used to send data or actively disconnect.

At this point, connect, disconnect, send and receive operations are non-blocking; open and close are blocking (for obvious reasons). 

Note that the same host object may be used to actively request connections and accept incoming connections all the same which makes it usable in P2P 
topologies. Client/server roles are not enforced and emerge simply by how a host is configured. A host may even send connection requests to multiple remote 
hosts simultaneously. 

**Examples**:

Host object instantiated to connect to a remote peer. The inner loop is responsible to ensure events don't accummulate to the next iteration.

```csharp
using (var host = new Host("MyHost")
{
    host.Open(IPEndPoint.Any, new Host.Settings(0));
    host.Connect(new IPEndPoint(IPAddress.Loopback, 1313), out Peer peer);

    ...

    while (true)
    {
        while (host.TryGetEvent(out Event e))
        {
            if (e.EventType == EventType.Data)
                Console.WriteLine($"DATA: {e.Peer} {e.Data}");
            else if (e.EventType == EventType.Connection)
                Console.WriteLine($"CONNECTED: {e.Peer}");
            else if (e.EventType == EventType.Disconnection)
            {
                Console.WriteLine($"DISCONNECTED: {e.Peer} {e.Reason}");
                return;
            }
        }

        Thread.Sleep(33);
    }
}
```

Host object instantiated to wait for up to 10 incoming connections. The inner loop is responsible to ensure events don't accummulate to the next iteration.

```csharp
using (var host = new Host("MyHost")
{
    host.Open(new IPEndPoint(IPAddress.Loopback, 1313), new Host.Settings(10));

    ...

    while (true)
    {
        while (host.TryGetEvent(out Event e))
        {
            if (e.EventType == EventType.Data)
                Console.WriteLine($"DATA: {e.Peer} {e.Data}");
            else if (e.EventType == EventType.Connection)
                Console.WriteLine($"CONNECTED: {e.Peer}");
            else if (e.EventType == EventType.Disconnection)
            {
                Console.WriteLine($"DISCONNECTED: {e.Peer} {e.Reason}");
                return;
            }
        }

        Thread.Sleep(33);
    }
}
```

## Documentation

### Motivation

This project dates back to 2015 when I came to Canada to study Video Game Design and Development at the Toronto Film School. The original motivation was to 
create a compilation of accessory classes that could be re-used in multiple [Unity3d](https://unity.com) projects. After a while, I started to research network
solutions for a prospect multiplayer game and the focus shifted towards designing a reusable network module. Initially, I approached the problem as a simple
matter of integrating UNet or any other suitable 3rd party library I could find at the time. Soon after, I started bumping into all sorts of problems from
broken assumptions to hidden implementation trade-offs. It was not uncommon to find inflated (almost misleading) feature lists, design incompatibilities or 
plain broken implementations. In particular, what bothered me most was that many aspects of the solutions seemed randomly arbitrary with little to no 
explanation of why that approach was preferred or a certain limit imposed. I would spend hours inspectig a project's source taking notes to figure out why 
something was the way it was only to realize later that another part of code was in direct contradiction. 

All this drove me into more work and eventually I decided to build a lightweight network library myself with a reasonable feature list that I could implement 
and verify. No rush, no deadlines. Just a genuine attempt to implement the best technical solution I could devise. 

Meanwhile, I graduated, went back to a full-time job and had to set this project aside. A year ago, after finding some old notes, I restored my archive of 
prototypes and decided to put together a comprehensive build with all the information I gathered so that not only other people could experiment with it but also
understand the way it worked and why.

### Modules

- **[Carambolas](Doc/README-Carambolas.md)**    
- **[Carambolas.Net](Doc/README-Carambolas.Net.md)**
- **[Carambolas.Unity](Doc/README-Carambolas.Unity.md)**
- **[Carambolas.Unity.Replication](Doc/README-Carambolas.Unity.Replication.md)**


## Building from source

The managed assemblies can be built in any platform with a compiler that supports C# 7.3 or higher.
Tests and accessory applications require netcore 2.2.

Native libraries can be built using [CMake](https://cmake.org/download/) with GCC or Visual Studio.

**Supported OS versions:**
- Windows 7 or higher
- Linux (kernel 4.4 or higher)
- macOS 10.12 or higher

For any other platform, or in the absence of a required native library, fallback code exists that although in general may be less efficient must be fully
functional and transparent.

All C# projects and build scripts are configured to store intermediate files and binaries under a *Build* folder located at the project root so builds can be 
easily inspected, verified and cleaned.

The code uses DllImport to bind native libraries. DllImport may always use Windows library names and will automatically add other platforms' prefixes/suffixes 
as required. For instance, Carambolas.Net.Native.dll, the net native library's name on Windows, becomes libCarambolas.Net.Native.dll.so on Linux 
and libCarambolas.Net.Native.dll.dynlib on MacOS. Build scripts already create the libraries under the proper names.

**Dependencies:**

*Carambolas* depends on [System.Memory.dll (>= 4.5.3)](https://www.nuget.org/packages/System.Memory/) which should be automatically fetched in the build process
and does not require manual intervention.

*Carambolas.Unity* and *Carambolas.Unity.Replication* depend on assemblies from the [Unity Engine](http://www.unity.com).

As redistributing such assemblies would represent a breach of the license agreement, I chose to add a dependency on [this great nuget package by DerploidEntertainment](https://www.nuget.org/packages/Unity3D)
that basically lets you refer to your own Unity installation. If you use UnityHub and installed unity in the default location nothing else should be needed.
If you installed unity in a custom location or have multiple unity versions you may want to define exactly which installation to refer to by declaring the 
following environment variables.

```
UnityInstallRoot=<installation root path>
UnityVersion=<unity version>
```

Environment variables make the build very flexible and can make your life much easier too. For example, in my windows machine I have UnityHub installed at 
its default location (on drive C:) but I keep multiple unity installations on a separate drive at D:\Unity. There I keep a symlink ([using `mklink /d`](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/mklink))
representing every major unity version I have got installed (i.e 2017, 2018, 2019, 2020) so the only thing I need to do after a unity update is to adjust my 
symlink. And in my system settings I declared the two environment variables as:

```
UnityInstallRoot=D:\Unity
UnityVersion=2019
```

And that's it. No one ever needs to touch those variables again.

### Windows 
 
A visual studio solution is included for convenience, so no additional build steps should be required for Windows. 
**Only make sure to select the platform corresponding to your host operating system (either x86 or x64).** This is required to build 
the test applications and for unit tests. All .NET assemblies are built for *AnyCPU* regardless of the solution platform selected, but visual studio must know 
what native libraries to build for testing as they're expected to be deployed side-by-side with their associated assemblies. 

Use [nugetpack.bat](nugetpack.bat) to compile the native library and portable assemblies and create NuGet packages all in a single action. 

Use [build.bat](build.bat) to build all projects for release without using Visual Studio.


### Mac 

Visual Studio for Mac hasn't been tested and is not supported, so don't expect it to work.

Make sure to have cmake (>= 2.8) and gcc to be able to compile native libraries.
Dotnet core SDK 2.1 is required to compile the assemblies and generate nuget packages.

Use [nugetpack.sh](nugetpack.sh) to compile the native library and portable assemblies and create NuGet packages all in a single action. 

Use [build.sh](build.sh) to build all projects for release without using Visual Studio.


### Linux

Make sure to have cmake (>= 2.8), build-essential and gcc-multilib installed to be able to compile native libraries for both x86 and x64. 

On Ubuntu run:
$ sudo apt-get install build-essential gcc-multilib g++-multilib cmake

Dotnet core SDK 2.1 is required to compile assemblies and generate nuget packages.

On Ubuntu run:

```
$ wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
$ sudo dpkg -i packages-microsoft-prod.deb
$ sudo apt-get update; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-2.1
```

Use [nugetpack.sh](nugetpack.sh) to compile the native library and portable assemblies and create NuGet packages all in a single action. 

Use [build.sh](build.sh) to build all projects for release without using Visual Studio.

## Unity "Gotchas"

**Use of compiler directives**

Unity defines a whole set of useful [compiler directives](https://docs.unity3d.com/Manual/PlatformDependentCompilation.html).
One of the most well known is `UNITY_EDITOR`. It's useful because many classes are only available in the Editor as well as certain assemblies. So it's not 
uncommon to see `#if UNITY_EDITOR` in scripts everywhere and Carambolas.Unity is not different. The problem with pre-compiled assemblies, however is that 
compiler directives must be provided ahead of time and there's now way to know which ones are actually going to be used in a Unity project. Therefore all 
Carambolas projects that depend on the Unity Engine also have a second corresponding project that includes all the original source code as links plus any 
editor only classes and is be compiled with the UNITY_EDITOR directive. For example, Carambolas.Unity produces Carambolas.Unity.dll and has a corresponding 
Carambolas.Unity-Editor project that is compiled with `UNITY_EDITOR` defined and produces Carambolas.Unity-Editor.dll. The former assembly is configured in 
Unity (in the meta file) for all platforms except Editor so it's included in builds but never while playing in the Editor. The latter is configured to the
opposite, available only in Editor and not for any other platform. This way carambolas classes that are meant to be included for both the Unity Player and the
Unity Editor may still use the idiomatic #if UNITY_EDITOR to adapt compilation for each case.

Note that all this refers to carambolas sources only and does interfere with user code or any third-party code in a Unity project.

For predefined compiler directives other than `UNITY_EDITOR` the issue is more complicated. We can't have an assembly version for each directive and even if
we had those there's no way to condition the use of an assembly to a compiler directive. The case for `UNITY_EDITOR` is very specific because it happens to be 
related to a platform. So it's not possible to refer to any other unity compiler directive in Carambolas source code (that is precompiled).

This is an issue in certain cases where the Unity API does not provide a static property but only a compiler directive to identify certain system properties.
Consider the Server Build option, for example. Different than a Development Build that can be identified in runtime by checking Debug.IsDebugBuild, there is 
no built in way to identify Server Builds in runtime. You may check Application.isBatchMode but they're not the same thing. So there's no Debug.isServerBuild. 
Fine. But there is a `UNITY_SERVER` directive that is provided by the compiler on server builds. Ok. But pre-compiled assemblies cannot do `#if UNITY_SERVER`
so now what? Well, one interesting alternative is to cleverly use the [Conditional Attribute](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.conditionalattribute).
For example, when a precompiled assembly has a class like this:
 
```c#
namespace Carambolas.UnityEngine
{
    public static class SomeClass
    {
        [Conditional("UNITY_SERVER")]
        public static void SomeMethod() 
        { 
            Debug.Log("This is a server build");
        }
    }
}
```

one may call SomeClass.SomeMethod() from any other assembly (CSharp-Assembly included) that was compiled with `UNITY_SERVER` defined and the call will 
produce the log the message. Otherwise the call will be completely ommited by the JIT. Sounds great, uh? Sort of a dynamic compiler directive. The gotcha is 
that the directive the condition is based on does not propagate, it's only evaluated in the compilation unit of the caller - the exact location. For example:

```c#
namespace Carambolas.UnityEngine
{
    public static class SomeClass
    {
        [Conditional("UNITY_SERVER")]
        public static void SomeMethod() 
        { 
            Debug.Log("This is a server build");
        }

        public static void OtherMethod() 
        {
            // Do something interesting here and then...

            SomeMethod();
        }
    }
}
```

If instead of calling SomeClass.SomeMethod() directly we decided to call SomeClass.OtherMethod() the log message would never be produced regardless of the 
caller's assembly being compiled with `UNITY_SERVER` or not. The reason is that the call to SomeMethod() is now originating from within the precompiled 
assembly itself (not the user's assembly) and it has not been compiled with `UINTY_SERVER` defined (obviously) thus the condition always fails. 

This situation with conditional methods may look really puzzling sometimes so the solution employed by Carambolas to capture information of the build context 
in Unity (such as isServerBuild, for example) is to leave an assembly to be compiled by Unity itself, namely Carambolas.Unity.Deferred. As the name implies, 
this assembly contains only code that depends on the build context of a Unity project and cannot be pre-compiled - hence the "deferred" qualifier. Assembly 
definition and source files are packed by [UnityPackageManager.Carambolas.Unity](UnityPackageManager.Carambolas.Unity/UnityPackageManager.Carambolas.Unity.csproj).


**Console input/output**

Unity applications have console input and output disabled so methods such as *System.Console.WriteLine* will silently fail even if the application is started in the 
command line with *-batchmode*. There are alternatives to attach a terminal window but they all require use of native system libraries. The only exception is for 
headless unity applications built with [BuildOptions.EnableHeadlessMode](https://docs.unity3d.com/ScriptReference/BuildOptions.EnableHeadlessMode.html) 
(equivalent to setting the *Server Build* checkbox in the build settings window). The resulting unity player in this case will have no visual elements without 
the need to specify any command line options (hence the *headless*), managed scripts will be compiled with the `UNITY_SERVER` compiler directive and stdin and 
stdout will be accessible (Unity will logs go to stdout by default). Mac and Linux players are compiled as a standard console application. Windows player is 
compiled with /SUBSYSTEM:Console and runs as a standard windows console application.


**Unity Package Manager (UPM) Projects:**

Projects with names starting with "UnityPackagerManager" are intended to build UPM packages and must not produce assemblies of their own. If you edit the
respective csproj files however you will notice I had to resort to a few tricks to work around some Visual Studio and MSBuild limitations such as: 

* Empty projects with no assembly info still produce a dll, a pdb and a deps.json file in the output path. The nuget packaging process is smart enough to 
ignore these files when the project has `<NoBuild>true</NoBuild>` hence why NuGet packages for runtimes only (such as [this one](Carambolas.Net.Native/nuget/Carambolas.Net.Native.Win.csproj))
do not end up with no-op assemblies. In the normal build process, however, one has to manually delete the undesired files using a post build task.

* Visual studio treats any file called package.json as an npm package manifest which poses a problem becase UPM being based on npm also uses a package.json.
Besides the format not being exactly the same, visual studio will by default try to automatically restore the packages described inside it when the project is 
open or the file is saved. The most common solution to this problem is to disable npm auto restore on Visual Studio settings. This is inconvenient because as 
a global setting it may adversely affect other unrelated projects. I opted for the alternative of naming the file differently and add it to the project using a link which 
will automatically produce the proper renaming of the output on build.

* Visual Studio insists on copying upper level dependencies over to the output path even when the immediate project reference is configured with Private 
= False. The only workaround I found besides deleting the files in a post build task was to also reference all indirect (upper level ) dependencies with 
Private = False.

* System dependencies introduced by NuGet package references in [Carambolas.csproj](Carambolas/Carambolas.csproj)) cannot be included side-by-side with 
Carambolas.dll in the same unity package or we risk unreconcilable conflicts with third-party packages. Unity cannot handle multiple assemblies with the same name in a single unity 
project regardless of how these assemblies ended up in there. This poses a problem because if any other third-party package or even user code is included that brings in its own System.Memmory.dll (or any of its dependencies) Unity will raise an
exception and refuse to build the project. In theory, until Unity provides an official System.Memory UPM package the best we can do it leave the user responsible 
for the management of third-party dependencies. In order to build a separate dotnet.system.memory package we created a csproj that sets 
`<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` references any other projects with known dependencies (in this case 
[Carambolas.csproj](Carambolas/Carambolas.csproj)) and deletes the resulting assembly (pdb and deps.json) leaving only the package reference dependencies which then
are copied to the Runtime subfolder.


## Testing

A minimum set of unit tests are implemented around key features using [xUnit](https://xunit.net) projects.

Carambolas.Net.Tests.Host is a simple console application used to manually verify basic network functionality. It's particular useful when sided with [Wireshark](#wireshark)
and [Clumsy](#clumsy)

Carambolas.Net.Tests.Integration is a set of integration tests for Carambolas.Net also implemented with [xUnit](https://xunit.net). Tests run sequentially, each 
starting two separate threads (a server and a client), that communicate over the loopback interface for a specific amount of time. The loopback represents an ideal 
network where round-trip time is minimum, packets never arrive out of order and are never lost unless there is a bufffer overflow. These characteristics are useful 
to validate normal execution paths.


## Resource Icons

All resource icons exported for use in the unity editor were obtained from [Material Design Icons](http://materialdesignicons.com) as PNG with 72px
background = transparent, foreground = #565656 (for normal skin) / #C2C2C2 (for pro skin), padding = 0, corner radius = 0. Art sources may be found 
at https://github.com/Templarian/MaterialDesign.


## Tools

### Wireshark

[Wireshark](https://wiki.wireshark.org/) is an invaluable debugging tool that can be used to monitor network activity and inspect packets. In addition, Wireshark 
supports a special class of plugins called [dissectors](https://wiki.wireshark.org/Lua/Dissectors) that can be used to analyze custom protocols. 

This project includes a basic [wireshark dissector for Carambolas](Tools/Wireshark/plugins/carambolas.lua). In order to use it, make sure Wireshark is already 
installed. 

- Copy the [dissector](Tools/Wireshark/plugins/carambolas.lua) file to %APPDATA%/Wireshark/plugins
- Open Wireshark (or if Wireshark is already open, press Ctrl+Shift+L to reload all lua scripts)
- Go to Help->About. Carambolas should be listed in the plugins tab.
- Go to Analyze->"Decode As". Click Add ("+"). 
  - In the new record set Field = UDP port; Value = 1313; Type = Integer, base 10; Default = (none); Current = CARAMBOLAS. 
  - Click Save and OK. This is going to make Wireshark automatically decode any udp packet on port 1313 as Carambolas. Port 1313 is the default port used in 
    integration tests and by the test host.
- In the capture window, you may now use "carambolas" to filter for only carambolas packets. Note that the "info" column only contains a hint of a packet's
  actual content. This is because a packet may contain more than on ACK, SEG or FRAG across multiple channels.
- Filter options include:
  - *carambolas.stm*: Source Time
  - *carambolas.secure*: Secure packet
  - *carambolas.ssn*: Source Session
  - *carambolas.rwd*: Receive Window
  - *carambolas.crc*: Checksum (of insecure packet)
  - *carambolas.pubkey*: Source Public key
  - *carambolas.encrypted*: Encrypted data 
  - *carambolas.nonce*: Nonce
  - *carambolas.mac*: MAC
  - *carambolas.connect*: Connect packet
  - *carambolas.connect.mtu*: Source Maximum Transmission Unit
  - *carambolas.connect.mtc*: Source Maximum Transmmission Channel
  - *carambolas.connect.mbw*: Source Maximum Bandwidth
  - *carambolas.accept*: Accept packet
  - *carambolas.accept.mtu*: Source Maximum Transmission Unit
  - *carambolas.accept.mtc*: Source Maximum Transmmission Channel
  - *carambolas.accept.mbw*: Source Maximum Bandwidth
  - *carambolas.accept.atm*: Acceptance Time
  - *carambolas.accept.assn*: Accepted Session*
  - *carambolas.messages*: Messages packet
  - *carambolas.reset*: Reset packet
  - *carambolas.qos*: Message QoS (Reliable, Semireliable, Unreliable)
  - *carambolas.chn*: Message Channel
  - *carambolas.seq*: Message Sequence Number
  - *carambolas.rsn*: Message Reliable Sequence Number
  - *carambolas.seglen*: Total Segment Length
  - *carambolas.fragindex*: Fragment Index
  - *carambolas.data*: Message Data
  - *carambolas.data.len*: Message Data Length
  - *carambolas.ping*: Ping message
  - *carambolas.segment*: Segment message
  - *carambolas.fragment*: Fragment message
  - *carambolas.ack*: Ack message
  - *carambolas.ack.accept*: Ack(Accept) message
  - *carambolas.ack.cnt*: Ack count
  - *carambolas.ack.next*: Ack next sequence number expected
  - *carambolas.ack.last*: Ack last sequence number expected (of a gap)
  - *carambolas.ack.gap*: Gap size
  - *carambolas.ack.atm*: Acknowledged Time


### Clumsy

[Clumsy](https://jagt.github.io/clumsy/) is a network packet capture program that runs in user-mode and is capable of intercepting packets to simulate degraded
network conditions in real-time.

Add a pre-set filter line like the following in the config.txt file to affect hosts connected by the loopback interface on the same port used in integration tests (1313):

```
carambolas: udp and outbound and loopback and (udp.DstPort == 1313 or udp.SrcPort == 1313)
```

Note that there are a few caveats when using Clumsy with the loopback interface. From the [Clumsy user manual](https://jagt.github.io/clumsy/manual.html):

> 1. Loopback inbound packets can't be captured or reinjected.
> When you think about it, it's really difficult to tell it's an inbound or outbound packet when you're sending packets from the computer to itself. In fact the underlying Windows Filtering Platform seems to classify all loopback packets as outbound. The thing to remember is that when you're processing on loopback packets, you can't have "inbound" in your filter. It's important to know that your computer may have IPs other than 127.0.0.1, like an intranet IP allocated by your router. These are also considered loopback packets.

> 2. Loopback packets are captured twice.
> Since we don't have inbound loopback packets, all loopback packets are considered as outbound. So clumsy will process them twice: first time is when sending, and second time when receiving. A simple example is that when filter is simply "outbound", and apply a lag of 500ms. When you ping localhost, it would be a lag of 1000ms. You can work around it by specify destination port and things like this. But it would be easier to just keep this in mind and be careful when setting the parameters.

> 3. Inbound packet capturing is not working all the time.
> As previously noted, loopback inbound packets can't be reinjected. The problem is that on occasions some packets may be classified as inbound packets, even if the destination IP isn't of your computer. This only affects non-loopback packets. If you're only working on localhost it's going to be fine. The goal of future release is to diagnose what caused this and provide a solution.

> 4. Can't filter based on process
> System wide network capturing is listed as a feature. But really this is since there's no easy way to provide a robust solution.


## Support this project

I'm always open to contributions, either in the form of bug reports, bug fixes (even better!) or improved test coverage. 

Feature requests are welcome but may be kept in a backlog depending on how extensive, feasible or desirable they are. If a feature request is too complex
it may depend on sponsorship as I have limited resources (time and money) to dedicate.

If you would like to support this project I may be interested in hearing from you, so reach out!


## FAQ

### Gereral 

##### Carambolas, what kind of name is that?

In portuguese, Carambolas is the plural form of [carambola](https://en.wikipedia.org/wiki/Carambola) (= starfruit). The term is also used colloquially in 
certain regions of Brazil to express astonishment or impatience. 

##### What's with the funny solution name?

Before Carambolas, I made at least a half-dozen attempts to organize my ideas in a usable project. With Carambolas I decided to build a series of prototypes 
in order to learn about design issues and test different approaches. Each prototype had a code name formed by a letter and a number starting at A1. The source 
code that was initially imported in this repository was the 75th iteration of the 9th prototype, hence A9.

##### I deployed my application without native libraries and it worked just fine. Are native libraries really needed?

Native libraries are provided mostly for performance reasons, therefore they're totally optional. It would have been unreasonable to try to provide a native 
implementation for every possible platform (think about all desktops, mobile, console, embedded...) and relying exclusively on native libraries would reduce 
target platforms to only a handful, possibly desktop only (windows, linux and macOS). So as a rule of thumb, there must always be a fallback implementation 
in managed code for any functionality implemented by a native library. 

Because native libraries are optional, the program can't tell whether a missing file was supposed to be there or not, hence why there's no error thrown or 
logged for a missing native library. By definition, a missing native library is NEVER an error.


##### How can I tell if a native library is actually being used then?

In general, you can't. And you shouldn't, at least not from an API perspective. It should not matter to the user (or app programmer) what underlying 
implementation strategy is employed by a dependecy, in this case Carambolas. However this information might be relevant for deployment so, every time an interop
object is created that also has an automatic fallback, the code produces an indicative log info. For instance, Carambolas.Net.Socket will produce a log info 
similar to "Using Carambolas.Net.Sockets.Native.Socket" when a native library is found for the underlying socket implementation. This way if you're deploying 
with native libraries in mind you can determine whether they're actually being used. 


##### Why my application is throwing System.BadImageFormatException?

This means you're deploying a native library that is either corrupt or compiled for the wrong CPU architecture.

Native libraries must go side-by-side with their corresponding interop assemblies and although assemblies may be compiled once for any CPU architecture, native
libraries cannot. They must match the CPU architecture of the running operating system, otherwise they're treated as corrupt files and .NET throws a 
System.BadImageFormatException. Note that this is not the same as trying to load a library that is not found which is by definition not an error.


##### What is all this x86, x86_64, x64 and Win32 terminology ?

Long story short these are code names used to identify CPU instruction sets and consequently a hardware platform (because in the past a CPU would only support a 
single set of instructions and even though nowadays CPUs may implement multiple sets there's always one identified as *the main one*.) 

About 30 years ago, the term x86 was used to denote a whole family of CPUs that implemented more or less the same instruction set development by Intel Corp.
which started with the 8086 CPU (learn more at [wikipedia](wikipedia.org/wiki/X86)). Later with the advent of 64-bit CPU architectures an interesting thing 
hapened. Intel pushed a new CPU architecture (Itanium) with a brand new instruction set tailored exclusively for 64-bit systems and this set was then referred 
to as x64. Nonetheless, at the same time AMD also produced and extended x86 instruction set for a hybrid 32/64-bit CPU [(Opteron and the like in 2003)](wikipedia.org/wiki/X86-64) 
which suddenly rendered the term x64 umbiguous. For a while x64 was exclusively used to refer to Itanium-like architectures and other terms were used for x86 
extended with 64-bit instructions. These terms included AMD64 and x86_64 and Itanium started to be refered to as i64. In time x86_64 was preferred largely due
to AMD being a trademark. After all, what company in the world would passively employ a competitor's brand name in technical terminology that was widely 
publicised? Anyway, adding to the confusion Microsoft started to refer to its Windows operating system and legacy APIs as Win32 while the 64-bit versions rather
than being called Win64 became known as Win32 x64. Yes, programmers are confused beasts. In another fron MacOS and Linux started supporting binaries that were 
compiled with two sets of instructions a pure x64 for 32-bit systems and a x86_64 for 64-bit systems running on those hybrid CPUs that had become the norm (yes,
huge Intel blunder with the CPU market at the time). These smart binaries were sometimes also referred to as being x86_64 which caused even more confusion.

Cutting to the chase, usually in Visual Studio solutions and in project files:

  * x86 refers to 32-bit operating systems running on a modern x86 CPU;
  * x64 refers to 64-bit operating systems running on a modern x86 CPU;
  * Win32 is a legacy term still employed by default in Microsft tools to refer 32-bit operating systems running on a modern x86 CPU;
  * AnyCPU refers to a high level instruction set that does not depend on the CPU architecture (such as dotnet IL) and is only employed for .NET assemblies;

In Unity on the other hand:

  * x86 refers to 32-bit operating systems running on a modern x86 CPU;
  * x86_64 refers to 64-bit operating systems running on a modern x86 CPU;


### Network


##### What is the bandwidth-delay product?

The bandwidth-delay product (BDP) is the product of a network link's transmission capacity (in bits per second) and its round-trip delay time (in seconds). It 
represents the very maximum amount of data that a network can retain before any acknowledgement may arrive. 

The BDP can be used to classify networks according to whether it is above or below a certain threshold. Networks with a large BDP are called Long Fat Networks
(LFNs). LFNs may be networks with a very large average round-trip time (regadless of bandwidth, as in satellite links) or a wide (high bandwidth) network that 
displays considerably small round-trip times (as in gigabit ethernet links).

Check the [wikipedia](https://en.wikipedia.org/wiki/Bandwidth-delay_product) for more information about it.


##### Why did you implement a Socket class of your own? Why can't you simply use the Socket class already available in .NET instead of trying to re-invent the wheel?

A Carammbolas.Net.Socket object serves as a facade to a native socket implementation or a fallback implementation that relies on System.Net.Sockets.Socket. It 
helps to decouple and reduce the complexity of Host and Peer objects. Refer to [Doc/README-Carambolas.Net](Doc/README-Carambolas.Net.md#socket) for more information.


##### Why did you implement a custom IPEndPoint and IPAddress? Why can't you simply use the classes already available in .NET instead of trying to re-invent the wheel?

System.Net.IPAddress and System.Net.IPEndPoint are mutable objects that promote a number of unecessary allocations in all current implemenations of .NET Core and .NET 
Framework. Carambolas.Net.IPAddress and Carambolas.Net.IPEndPoint are immutable value types that contribute to reduce GC pressure. Refer to 
[Doc/README-Carambolas.Net](Doc/README-Carambolas.Net.md#ipendpoint-and-ipaddress) for more information.


##### Is encryption method *xyz* supported?

[AEAD](https://en.wikipedia.org/wiki/Authenticated_encryption) with [ChaCha20](https://en.wikipedia.org/wiki/Salsa20#ChaCha_variant) and [Poly1305](https://en.wikipedia.org/wiki/Poly1305)
is supported out-of-the-box. Custom strategies may be implemented by providing the host with implementations of Carambolas.Net.ICipher and Carambolas.Net.ICipherFactory
interfaces. The only requirements are:

- key size must  be 256 bits;
- mac size must be 128 bits;
- encryption method must be format-preserving (so the total packet length is not affected)


##### Is data compression supported?
 
A user application is free to compresss its data before sending but there's currently no mechanism to provide automatic compression/decompression of either individual 
messages or complete packets.

### Unity

##### What is this magic number unity complains about when I try to run my server build in a linux terminal?

This has nothing to do with Carambolas but it's still a very common issue. 

When running a headless server build, Unity does two main things differently: first, it disables rendering by creating a Null GfxDevice (like when you run a unity
app with -batchmode); and second, it tries to attach the debug log to the console output. This latter step is the issue. Mono checks the type of linux terminal in 
use against a hardcoded type id (the magic number 542) which represents xterm. If you are seeing an error like this

```
Exception: Magic number is wrong: 542
  at System.TermInfoReader.ReadHeader (System.Byte[] buffer, System.Int32& position) [0x00028] in <2b3a3162be434770b7a4fac8b896e90c>:0
  at System.TermInfoReader..ctor (System.String term, System.String filename) [0x0005f] in <2b3a3162be434770b7a4fac8b896e90c>:0
  at System.TermInfoDriver..ctor (System.String term) [0x00055] in <2b3a3162be434770b7a4fac8b896e90c>:0
  at System.ConsoleDriver.CreateTermInfoDriver (System.String term) [0x00000] in <2b3a3162be434770b7a4fac8b896e90c>:0
  at System.ConsoleDriver..cctor () [0x0004d] in <2b3a3162be434770b7a4fac8b896e90c>:0
Rethrow as TypeInitializationException: The type initializer for 'System.ConsoleDriver' threw an exception.
  at System.Console.SetupStreams (System.Text.Encoding inputEncoding, System.Text.Encoding outputEncoding) [0x00007] in <2b3a3162be434770b7a4fac8b896e90c>:0
  at System.Console..cctor () [0x0008e] in <2b3a3162be434770b7a4fac8b896e90c>:0
Rethrow as TypeInitializationException: The type initializer for 'System.Console' threw an exception.
  at UnityEngine.UnityLogWriter.Init () [0x00006] in <35ac225908204e43a83851058e9e621c>:0
  at UnityEngine.ClassLibraryInitializer.Init () [0x00001] in <35ac225908204e43a83851058e9e621c>:0

(Filename: <2b3a3162be434770b7a4fac8b896e90c> Line: 0)
```

then your terminal env must have a TERM variable value other than xterm. In Windows 10 WSL for instance, TERM is set to xterm-256color which is not what
Unity can handle. A simple workaround is to execute your unity server build in linux as `$> TERM=xterm ./myunityserverbuild`


## License

Carambolas and all its constituent components including both source code and compiled forms are licensed under the [MIT License](LICENSE).
Unless expressly provided otherwise, the Software under this license is made available strictly on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. 
Please review the license for details on these and other terms and conditions.

### Third Party Notices

*Carambolas.CommandLineArguments* was based on an [article by GriffonRL (Richard Lopes)](http://www.codeproject.com/KB/recipes/command_line.aspx) with source code published 
under an [MIT license](LICENSE-CommandLine) with ideas from an [article by Jake Ginnivan](http://jake.ginnivan.net/c-sharp-argument-parser/) 
that expanded on the original source.

*Carambolas.Security.Criptography.Crc32c* was based on [Crc32.Net 1.0 (fbc1061b0cb53df2322d5aed33167a2e6335970b) by force](https://github.com/force-net/Crc32.NET) 
under an [MIT license](LICENSE-Crc32.Net).

*Carambolas.Security.Criptography.NaCl* was based and expanded on [NaCl.Core 1.2 (a9f09c01fceb5b47bca5256518e848afc860acea) by David De Smet](https://github.com/daviddesmet/NaCl.Core) 
under an [MIT license](LICENSE-NaCl.Core).

*Carambolas.Unity depends on the [Unity Engine](http://unity.com) thus being subject to the [Unity Companion License](LICENSE-Unity)

*Carambolas.Unity-Editor depends on the [Unity Engine](http://unity.com) thus being subject to the [Unity Companion License](LICENSE-Unity)

*Carambolas.Unity.Replication depends on the [Unity Engine](http://unity.com) thus being subject to the [Unity Companion License](LICENSE-Unity)

*Carambolas.Unity.Replication-Editor depends on the [Unity Engine](http://unity.com) thus being subject to the [Unity Companion License](LICENSE-Unity)

*Resource icons* exported for use in the unity editor were provided by [Material Design Icons](http://materialdesignicons.com) under the [Pictogrammers Free License](LICENSE-MaterialDesignIcons)

* The [protocol dissector](Tools/Wireshark/plugins/carambolas.lua) written in lua for [Wireshark](https://www.wireshark.org) is available under a 
[GPLv3 license](Tools/Wireshark/plugins/LICENSE). It's only supposed to be used as an input file for Wireshark in order to extend it's capabilities and allow 
it to display more information about UDP packets formatted according to the carambolas network protocol. Therefore it's completely separate and does not interact, 
depend or contribute in any way to Carambolas source files, assemblies or native libraries.
