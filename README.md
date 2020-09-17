# Carambolas

Carambolas is an ever evolving general purpose support library comprised of multiple [**.NET Standard 2.0**](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard2.0.md)
assemblies. In particular, <ins>it features a custom multi-channel reliable UDP protocol implementation intended for low latency/low [bandwidth-delay product](#what-is-the-bandwidth-delay-product)
network applications</ins>.

The first release is a minimal core with a fully functional network module. This repository is structured around a single solution because I plan to expand 
by adding new modules in the future.

Test coverage is still minimal so no level of correctness or performance should be implied without a close inspection of the source code. This is an on-going
project in its earliest stages. 

## Binaries

Binaries are available in the [archive section]((https://github.com/nlebedenco/Carambolas/releases)) or in the form of [nuget packages](https://www.nuget.org/packages/carambolas/). 

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
broken assumptions to hidden implementation trade-offs. It was not uncommon to find inflated (almost misleading) feature lists out there. Design
incompatibilities or plain broken implementations of what could have otherwise been considered good concepts were not unusual either. In particular, what
bothered me most was that in many solutions certain aspects seemed randomly arbitrary with little to no explanation of why a specific approach was preferred, 
or a certina limit imposed. I would spend hours inspectig a project's source code taking notes to find how and why something was implemented only to realize 
later that it was in direct contradiction to another (supposedly intentional) assumption made by the library's developer somewhere else. 

All this drove me into more work and eventually I decided to build a lightweight network library myself with a reasonable feature list that I could implement 
and verify. No rush, no deadlines. Just a genuine attempt to implement the best technical solution I could devise. 

Meanwhile, I graduated, went back to a full-time job and had to set this project aside. A year ago, after finding some old notes, I restored my archive of 
prototypes and decided to put together a comprehensive build with all the information I gathered so that not only other people could experiment with it but also
understand the way it worked and why.

### Modules

- **[Carambolas](Doc/README-Carambolas.md)**    
- **[Carambolas.Net](Doc/README-Carambolas.Net.md)**


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


### Windows 
 
A visual studio solution is included for convenience, so no additional build steps should be required for Windows. 
**Only make sure to select the platform corresponding to your host operating system (either x86 or x64).** This is required to build 
the test applications and for unit tests. All .NET assemblies are built for *AnyCPU* regardless of the solution platform selected, but visual studio must know 
what native libraries to build for testing as they're expected to be deployed side-by-side with their associated assemblies. 

Use [nugetpack.bat](nugetpack.bat) to compile the native library and portable assemblies and create NuGet packages all in a single action. 

Use [build.bat](build.bat) to build all projects for release without using Visual Studio.


### Mac 

Visual Studio for Mac hasn't been tested and is not supported, so don't expect it to work.

Make sure to have cmake (>= 2.8) and gcc to be able to compile the native library.
Dotnet core SDK 2.1 is required to compile the assemblies and generate nuget packages.

Use [nugetpack.sh](nugetpack.sh) to compile the native library and portable assemblies and create NuGet packages all in a single action. 

Use [build.sh](build.sh) to build all projects for release without using Visual Studio.


### Linux

Make sure to have cmake (>= 2.8), build-essential and gcc-multilib installed to be able to compile the native library for both x86 and x64. 

On Ubuntu run:
$ sudo apt-get install build-essential gcc-multilib g++-multilib cmake

Dotnet core SDK 2.1 is required to compile assemblies and generate nuget packages.

On Ubuntu run:
$ wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
$ sudo dpkg -i packages-microsoft-prod.deb
$ sudo apt-get update; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-2.1

Use [nugetpack.sh](nugetpack.sh) to compile the native library and portable assemblies and create NuGet packages all in a single action. 

Use [build.sh](build.sh) to build all projects for release without using Visual Studio.


## Testing

A minimum set of unit tests are implemented around key features using [xUnit](https://xunit.net) projects.

Carambolas.Net.Tests.Host is a simple console application used to manually verify basic network functionality. It's particular useful when sided with [Wireshark](#wireshark)
and [Clumsy](#clumsy)

Carambolas.Net.Tests.Integration is a set of integration tests for Carambolas.Net also implemented with [xUnit](https://xunit.net). Tests run sequentially, each 
starting two separate threads (a server and a client), that communicate over the loopback interface for a specific amount of time. The loopback represents an ideal 
network where round-trip time is minimum, packets never arrive out of order and are never lost unless there is a bufffer overflow. These characteristics are useful 
to validate normal execution paths.


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

[Clumsy](https://jagt.github.io/clumsy/) is network packet capture program that runs in user-mode and is capable of intercepting packets to simulate degraded
network conditions in real-time.

Add a preset filter line like the following in the config.txt file to affect hosts connected by the loopback interface on the same port used in integration tests (1313):

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


##### I deployed my application without native libraries and it worked just fine. Are native libraries really needed?

Native libraries are provided mostly to improve performance, therefore they're totally optional. It would have been unreasonable to try to provide a native 
implementation for every possible platform (think about all desktops, mobile, console, embedded...) and relying exclusively on native libraries would reduce 
target platforms to only a handful, possibly desktop only (windows, linux and macOS). So there's always going to be a fallback in managed code for any 
functionality implemented by a native library. 

Because native libraries are optional, the program can't tell whether a missing file was supposed to be found or not, hence why there's no error thrown or 
logged for a missing native library. By definition, a missing native library is NEVER an error.


##### How can I tell if a native library is actually being used?

In general, you can't. And you shouldn't, at least not from an API perspective. It should not matter to the user what underlying implementation strategy
is employed. However this information might be relevant for deployment, so every time an interop object is created that also has an automatic fallback, the code 
produces an indicative log info. For instance, Carambolas.Net.Socket will produce a log info similar to "Using Carambolas.Net.Sockets.Native.Socket" when a native 
library is found for the underlying socket implementation. This way if you're deploying with native libraries in mind you can determine whether they're actually 
being used. 


##### Why my application is throwing System.BadImageFormatException?

This means you're deploying a native library that is either corrupt or compiled for the wrong CPU architecture.

Native libraries must go side-by-side with their corresponding interop assemblies and although assemblies may be compiled once for any CPU architecture, native
libraries cannot. They must match the CPU architecture of the running operating system, otherwise they're treated as corrupt files and .NET throws a 
System.BadImageFormatException. Note that this is not the same as trying to load a library that is not found which is by definition not an error, since native 
libraries are always optional. 


### Network


##### What do you mean by short thin networks?

I don't know if this the appropriate terminology as I don't remember ever seeing it in the literature but I use it in opposition to Long Fat Neworks (aka LFNs)
which is in turn widely used in the liretature. LFNs are networks that display a bandwidth-delay product above a certain threshold.


##### What is the bandwidth-delay product?

The bandwidth-delay product (BDP) is the product of a network link's transmission capacity (in bits per second) and its round-trip delay time (in seconds). It 
represents the very maximum amount of data that a network can contain before any acknowledgement may arrive. 

The BDP can be used to classify networks according to whether it is above or below a certain threshold. Networks with a large BDP are called Long Fat Networks
(LFNs). Their opposite would be Short Thin Networks (STNs), that is networks with a small BPD. 

LFNs may be networks with a very large average round-trip time (regadless of bandwidth, as in satellite links) or a wide (high bandwidth) network that has 
considerably small round-trip times (as in gigabit ethernet links).

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


## License

All the source code and any binaries produced to be deployed alongside a user application are licensed under an [MIT license](LICENSE).

The [protocol dissector](Tools/Wireshark/plugins/carambolas.lua) written in lua for [Wireshark](https://www.wireshark.org) is available under a 
[GPLv3 license](Tools/Wireshark/plugins/LICENSE). It's only supposed to be used as an input file for Wireshark in order to extend it's capabilities and allow 
it to display more information about UDP packets formatted according to the carambolas network protocol. Therefore it's completely separate and does not interact, 
depend or contribute in any way to any source files, assemblies or native libraries.
