# Carambolas.Net


## Disclaimer

This library and its associated protocol definitions are not meant to be ultra-fast, ultra-lightweight, better than *xyz* or any other superlative. 
In a sense because there's no such thing as a definitve network solution. Behind any advertised set of features, there's always a list of pre-conditions, 
boundaries and assumptions. I tried my best to make them clear both in the source code and in the documentation. 

This is not a formal research project so although I seek to back design choices with sound arguments and I may sometimes cite open standards or someone else's 
research, no effort was made to formally prove hypotheses beyond an intuitive explanation.


## Introduction

Carambolas.Net is a reliable UDP networking protocol implemented in C# for user applications with soft real-time constraints that prioritize latency minimization 
and only require a small bandwidth-delay product (of one or two orders of mangnitude), i.e. rapid exchange of small payloads (64KB or less). Examples include 
simulations, multiplayer video games and sensor networks. 


## Main features 

- Session based protocol (i.e. connection oriented);
- Support for P2P topologies (the same host can initiate and accept multiple connections concurrently);
- Message oriented;
- 4 levels of reliability individually defined per send operation - also referred to as quality-of-service (QoS);
- Up to 16 independently sequenced channels;
- Round-trip time estimation;
- Keep-alive (aka ping);
- Limited congestion control/avoidance;
- Limited bandwidth control;
- Automatic fragmentation/reassembly of payloads up to 65535 bytes;
- Ability to handle half-open connections (limited in secure sessions);
- Conscious memory management to minimize GC pressure;
- Optional native library for improved performance/memory management;
- Optional encryption (out-of-the-box [ECIES](https://en.wikipedia.org/wiki/Integrated_Encryption_Scheme) with [Curve25519](https://en.wikipedia.org/wiki/Curve25519); 
[AEAD](https://en.wikipedia.org/wiki/Authenticated_encryption) with [ChaCha20](https://en.wikipedia.org/wiki/Salsa20#ChaCha_variant)/[Poly1305](https://en.wikipedia.org/wiki/Poly1305));


## Related work

Many features and certain design traits of Carambolas.Net are shared between other network solutions to date, most notably:

* [TCP](https://tools.ietf.org/html/rfc793)
* [SCTP](https://tools.ietf.org/html/rfc4960)
* [Enet](https://github.com/lsalzman/enet)/[Enet-CSharp](https://github.com/nxrighthere/ENet-CSharp)
* [LiteNetLib](https://github.com/RevenantX/LiteNetLib)
* [Lindgren-gen3](https://github.com/lidgren/lidgren-network-gen3)

I would encourage the reader to consider using either [TCP](https://tools.ietf.org/html/rfc793) or [SCTP](https://tools.ietf.org/html/rfc4960) before trying 
Carambolas.Net on a serious project. In part, because on the one hand you will be in a better position to identify your most relevant requirements, which may 
end up not being this library's main focus; and on the other, because the operating system will be handling a lot of the complications in a way that is arguably
more efficient than any user-space implementation could do.


## Normal operating conditions

What should be considered real-life normal operation for assessing correctness and performance? According to 
[a study conducted by Bungie for Halo 3 Beta](https://www.gamasutra.com/blogs/MarkMennell/20140929/226628/Making_FastPaced_Multiplayer_Networked_Games_is_Hard.php), 
in 2007, 99% of Xbox users displayed the following metrics (or better):

* 200ms one-way latency (i.e. 400 rountrip) between any two peers with 10% jitter;
* 8 KB/s bandwidth up and 8 KB/s down;
* up to 5% packet loss;

## Basic Concepts

In this section, the reader will find listed a few basic network concepts that the remainder of this document will repeatedly refer to. Citations are 
accompanied by a link to the source material. 

### Datagram
  
As described by [the wikipedia](https://en.wikipedia.org/wiki/Datagram):

> A datagram is a basic transfer unit associated with a packet-switched network. Datagrams are typically structured in header and payload sections. Datagrams
provide a connectionless communication service across a packet-switched network. The delivery, arrival time, and order of arrival of datagrams need not be 
guaranteed by the network. 

Datagrams have definite boundaries which are honored upon receipt, meaning a read operation at the receiver socket will yield an entire message as it was 
originally sent.

A UDP datagram is carried in a single IP packet and is hence limited to a maximum payload of:

    65535 - 60 (Max IPv4 header size) - 8 (UDP header size) = 65467 bytes for IPv4
    65535 - 40 (IPv6 header size)     - 8 (UDP header size) = 65487 bytes for IPv6
  

### IP Level Fragmentation
  
The transmission of large IP packets usually requires IP fragmentation. Fragmentation decreases communication reliability and efficiency and should
therefore be avoided. [This article](https://blog.cloudflare.com/ip-fragmentation-is-broken/) by *Marek Majkowski* presents some of the issues involved.
The author cites [Kent and Mogul, 1987](https://www.hpl.hp.com/techreports/Compaq-DEC/WRL-87-3.pdf) offering a few highlights:

> * To successfully reassemble a packet, all fragments must be delivered. No fragment can become corrupt or get lost in-flight. 
> There simply is no way to notify the other party about missing fragments.
>
> * The last fragment will almost never have the optimal size. For large transfers this means a significant part of the traffic will be composed of 
> suboptimal short datagrams - a waste of precious router resources.
>
> * Before the re-assembly a host must hold partial, fragment datagrams in memory. This opens an opportunity for memory exhaustion attacks.
>
> * Subsequent fragments lack the higher-layer header. TCP or UDP header is only present in the first fragment. This makes it impossible for firewalls 
> to filter fragment datagrams based on criteria like source or destination ports.

The largest IPv4 datagram that can be guaranteed to never be fragmented is very small - as per [RFC 791](https://tools.ietf.org/html/rfc791):
  
> Every internet module must be able to forward a datagram of 68 octets without further fragmentation. This is because an internet header may be up to 
> 60 octets, and the minimum fragment is 8 octets."

However, it is very unlikely for any path through the Internet to hit a link with such a low MTU. 

IPv6 is more demanding and requires every link to support an MTU of at least 1280 octets; routers are free to use links with physically smaller MTUs but
must reassemble any fragments before forwarding a complete IPv6 frame again.
     
Generally we can expect physical links in use in the public Internet to have MTUs of 1500 octets. This value is the default MTU for 802.3 Ethernet, 
[RFC 1042](https://www.ietf.org/rfc/rfc1042.txt), although there are extensions to support much larger MTUs, such as 9,000 octets (so called jumbo frames). On 
the other hand, the way that IP is actually carried over in such links often involves tunnels of various kinds, such as VLANs, MPLS, and VPNs; these all add 
small amounts of overhead to each packet, and so the MTU is often anything from 4 to 12 or more octets smaller than 1500. In practice, there's no easy way to 
reliably determine the MTU for a path. Path MTU Discovery (PMTUD) is the closet we can get but it's a complex subject with a lot of subtleties.

A sender can set the DF (Don't Fragment) flag in the IPv4 header, asking intermediate routers to never fragment a packet. Instead a router faced with a smaller 
MTU will send an ICMP packet back (ICMP Fragmentation needed, Type 3, Code 4) informing the sender to reduce the MTU for this connection. IPv6 does not support
fragmentation and consequently doesn't have the "Don't Fragment" option. In this case, every packet behaves as if DF=1. Many firewalls, however, will block ICMP 
packets as they pose a security risk, this includes the control messages that are necessary for the proper operation of PMTUD.
  
**What would be a "safe" UDP packet payload size to use then ?**

In IPv4, hosts are required to be able to reassemble IP frames up to 576 octets in length; in IPv6, the minimum is the same as the minimum MTU of 1280 octets.
In practice, most hosts can reassemble much larger IP frames. So the answer to the opening question would be a value that avoids any fragmentation. 
Unfortunately, this is simply not practical over IPv4, as this leaves us with only 8 bytes!
  
PMTUD is the best way to minimize the likelihood of fragmentation. Even so, a packet may still end up being fragmented if routing updates change the path to 
include a link with a smaller MTU after the packet has been dispatched by the source.
  
With that in mind maybe "safe" should be replaced with "guaranteed to be able to be reassembled, if fragmented", to which the answer is:
  
    576 - 60 (max IPv4 header) - 8 (UDP header) = 508 bytes for IPv4;
    1280 - 40 (IPv6 header) - 8 (UDP header) = 1232 bytes for IPv6.
  

### Time-To-Live (TTL)
  
The IPv4 RFC states that `TTL` is measured in seconds but acknowledges this is an imperfect measure. There is no way of knowing how long any particular host will
take to process a packet and most will do so in far less than a second. Based on this assumption, in theory the maximum time a packet can exist in the network 
is aproximately 4.25 min (255 seconds). 

[RFC2460 (IPv6)](#https://www.ietf.org/rfc/rfc2460.txt) is more conservative:

>Unlike IPv4, IPv6 nodes are not required to enforce maximum packet lifetime.  That is the reason the IPv4 "Time to Live" field was
>renamed "Hop Limit" in IPv6.  In practice, very few, if any, IPv4 implementations conform to the requirement that they limit packet
>lifetime, so this is not a change in practice.  Any upper-layer protocol that relies on the internet layer (whether IPv4 or IPv6) to
>limit packet lifetime ought to be upgraded to provide its own mechanisms for detecting and discarding obsolete packets.

### The case for CRC
  
**TL;DR**: CRC should always be used.
  
As pointed out by *Evan Jones* [in his article](http://www.evanjones.ca/tcp-and-ethernet-checksums-fail.html)
  
> TCP [and UDP] checksum is two bytes long, and can detect any burst error of 15 bits, and most burst errors of 16 bits (excluding switching 0x0000 and 0xffff).
> This means that to keep the same checksum, a packet must be corrupted in at least two locations, at least 2 bytes apart. If the chance is purely random, we
> should expect approximately 1 in 65536 (approximately 0.001%) of corrupt packets to not be detected. This seems small, but on one Gigabit Ethernet
> connection, that could be as many as 15 packets per second. For details about how to compute TCP/IP checksums and its error properties, see [RFC 768](http://www.faqs.org/rfcs/rfc768.html) 
> and [RFC 1071](https://tools.ietf.org/html/rfc1071). Also bear in mind that TCP [and UDP] checksum is optional and can either be disabled or not supported at
> all by a certain platform.
>  
> The Ethernet CRC is substantially stronger, partly because it is twice as long (4 bytes), and partly because CRCs have "good" mathematical properties(...) 
> [802.3 CRC] can detect up to 3 bit errors in a 1500 byte frame (see http://users.ece.cmu.edu/~koopman/networks/dsn02/dsn02_koopman.pdf). It appears
> that most switches discard packets with invalid CRCs when they are received, and recalculate the CRC when a packet goes back out. This means the CRC really only
> protects against corruption on the wire, and not inside a switch or any other type of intermediary network node. Why not just re-send the existing CRC then?
> Modern switch chips have features that modify packets, such as VLANs or explicit congestion notification. Hence, it is simpler to always recompute the
> CRC. For a detailed description, see [Denton Gentry's description](https://codingrelic.geekhold.com/2009/11/ethernet-integrity-or-lack-thereof.html)
> of how the Ethernet CRC doesn't protect very much.
>
> There is also one small complication that does not change the cause of failure but does change how you might detect it. Some switches support cut-through
> switching, where packets begin being forwarded as soon as the destination address is read, without waiting for the entire packet. In this case, it is already
> sending the packet before it can validate it, so it absolutely cannot recalculate the CRC. These switches typically support something called
> "CRC stomping" to ensure the outgoing CRC is invalid, so the ultimate receiver will eventually discard it. This gets more complicated when a destination port 
> is being used when a new packet arrives. In this case, cut-through switches must buffer packets, and then act like a store-and-forward switch. Hence,
> cut-through switching does not prevent switches from corrupting packets and appending a valid Ethernet CRC.
>  
> See [Cisco's white paper] (https://www.cisco.com/c/en/us/products/collateral/switches/nexus-5020-switch/white_paper_c11-465436.html) 
> on cut-through switching and [Cut-through, corruption and CRC-stomping] (http://thenetworksherpa.com/cut-through-corruption-and-crc-stomping/)
> for more details.
>  
> The conclusion is that when transmitting or storing data, you should always include strong CRCs that protect the data all the way from the sender to the
> final receiver.

### Automatic Repeat Request/Query Protocols

Automatic Repeat Request, also known as Automatic Repeat Query (ARQ), is an error-control method for data transmission that uses acknowledgements 
(confirmations sent by the receiver indicating that it has correctly received a message) and timeouts (specified periods of time allowed to elapse if an 
acknowledgment is not received) to achieve reliable data transmission over an unreliable service. If the sender does not receive an acknowledgment before 
the timeout, it usually re-transmits the message until an acknowledgment is received or either a predefined number of retransmission attempts or a 
predefined total time is exceeded.

### Selective Repeat Protocols

[Selective repeat](https://en.wikipedia.org/wiki/Selective_Repeat_ARQ) is a subset of ARQ protocols in which the sender is allowed to transmit the next message 
in a sequence within a certain range (called a window) without having to wait for individual acknowledgements. The receiver is capable of receiving messages 
out of order, detect missing messages and issue a request for specific messages to be retransmistted.

### Sliding Window Protocols

Sliding Window Protocols encompass all forms of ARQ protocols. From [the wikipedia](https://en.wikipedia.org/wiki/Sliding_window_protocol):

> A sliding window protocol is a feature of packet-based data transmission protocols. Sliding window protocols are used where reliable in-order delivery of packets
> is required, such as in the data link layer (OSI layer 2) as well as in the Transmission Control Protocol (TCP). They are also used to improve efficiency when 
> the channel may include high latency.
> 
> Packet-based systems are based on the idea of sending a batch of data, the packet, along with additional data that allows the receiver to ensure it was 
> received correctly, perhaps a checksum. When the receiver verifies the data, it sends an acknowledgment signal, or "ACK", back to the sender to indicate it 
> can send the next packet. In a simple automatic repeat request protocol (ARQ), the sender stops after every packet and waits for the receiver to ACK. This 
> ensures packets arrive in the correct order, as only one may be sent at a time.
> 
> The time that it takes for the ACK signal to be received may represent a significant amount of time compared to the time needed to send the packet. In this case, 
> the overall throughput may be much lower than theoretically possible. To address this, sliding window protocols allow a selected number of packets, the window, to 
> be sent without having to wait for an ACK. Each packet receives a sequence number, and the ACKs send back that number. The protocol keeps track of which packets 
> have been ACKed, and when they are received, sends more packets. In this way, the window slides along the stream of packets making up the transfer.


## Protocol Specification

### Considerations

* In order to send data to a remote host one doesn't need a sophisticated protocol, just a UDP socket open on both ends will do. The sender must know the IP 
address and port (also referred to as IP end point) of the destination. The receiver may identify a sender by IP address and port as well. The need for a more 
elaborate protocol becomes aparent when this communication needs to be coordinated.

* Network systems are inherently a form of coperative distributed system. Both sender and receiver are expected to cooperate for the communication to be 
effective. In principle, a node cannot force data onto another as much as it cannot forcebly retrieve data from another. This cooperation may be abused, however, 
leading to all sorts of degenerate states and ultimately disrupting the network. Moreover, a network peer must remain aware of other unrelated peers that might 
be sharing the same network resources despite never actively communicating with it. This situation may be approached as a game where each node wants to maximize
its own performance. [Shah et al](https://www.kau.edu.sa/Files/611/Researches/62804_33828.pdf) provides an overview of how game theory may be used to model 
communication networks.

* As described in [a previous section](#datagram), the maximum amount of user data that can be transmitted in a single send operation using a UDP transport is 
theoretically 65535 bytes minus the transport protocol overhead (UDP/IP). The chances of this data actually reaching the destination, however, are directly 
affected by the minimum `MTU` of the network path. 

***Carambolas.Net works under the assumption that the network is relatively stable.*** 

* Network nodes are assumed to remain online for relatively longer than the time a user program operates on both ends. Paths must appear mostly
unchanged with both datagram loss and out of order delivery events being rare compared to the amount of datagrams transfered. In these terms, it's considerably 
simpler to estimate a constant `MTU` with a safe margin of error than trying to implement PMTUD. Thus, the maximum payload unit (`MPU`), that is the maximum 
amount of user data that can be effectively transmimtted, is given by `MTU` - `[IP Header]` - `[UDP Header]`.

* Protocol overhead is the ratio between the amount of user data and the total number of bytes actually transmitted to carry this much data. It is minimum when 
the sender transmits a full `MPU` and maximum when it transmits a single byte. Under UDP/IP, assuming 48 bytes of protocol headers (estimated IP header of 40 
bytes + 8 bytes of UDP header) minimum overhead is `MPU`/48 and maximum overhead is 48 (i.e. 4800%);

* Since it's impossible to predict the average payload size of all the potential user applications we may try to batch multiple data segments together in a single
datagram in order to maximize datagram occupation;. In TCP/IP this problem is address by the [Nagle Algorithm](https://en.wikipedia.org/wiki/Nagle%27s_algorithm) 
which also employs a timer. Since TCP is trying to maximize throughput it's reasonable to wait a *certain* amount time for more data to fill up a packet. Just 
enough until waiting anymmore would affect throughput more than the packet overhead.

***Carambolas.Net wants to minimize latency, not necessarily maximize throughput.*** 

* Traffic generated by user applications are expected to follow a bursty model, but also to remain way below the link capacity on average (i.e the maximum 
throughput achievable for the link) so there's little to gain from waiting for data to accumulate. 

* Even so, a user application may be capable of producing multiple data segments in succession on a short period of time before the link could absorb them (a 
transmission peak). Each of these segments may be considered an independent [message](#messages). Multiple buffered messages could then be grouped together in 
the same datagram, provided there's enough space, in order to reduce overhead. 

* Datagram boundaries are not enough to promote data segmentation at the destination once there may be multiple user data segments per packet. This poses a 
problem of determining where each segment begins and ends. Usage of a starter or terminator sequence is less than ideal because data is binary and it would 
require either escaping or a very long terminator sequence introducing overhead and uncertainty. It's simpler to prefix every segment with its size and because
we know that `MPU` < 65535, the segment size only requires two bytes. This incurs a certain overhead but it's still better than not batching messages all. Now 
[the case for CRC](#the-case-for-crc) is even more compelling as we don't want to read half a message or beyond one into the next by accident. Minimum overhead 
becomes `MPU`/54, and maximum overhad, 54x, but in the average datagram occupation must be improved without requiring the user application to amalgamate data 
segments on its own.

* An IP address is used to identify a network node and a port number in the range 0-65535 is used to identify a process within that node (i.e. a user application)
however: 

  - Multiple hosts behind the same gateway may appear to have the same end point (the gateway's) to a remote host outside of that subnet;
  - Multiple processes in the same node may have been explicitly bound to the same local port (this is rare but possible, in which case a port is not enough to 
    identify a process anymore and whatever bound process reads first is the one that is going to receive the datagram);
  - Because there's only 65536 ports (and some are reserved) a process may reuse the same port a previously terminated process was using and eventually receive 
    a datagram intended for some other (possibly defunct) process;
  - The concept of an end-to-end [connection](#connection) must be used to match hosts that can talk to each other and ignore datagrams from unknown hosts;
  - This implies the need for at least two types of packets: one used to exchange control information and establish a connection and a second type used to
    batch messages and transmmit user data. This means an overhead of at least 1 bit per packet. In practice, there's more than only two types of packets so a
    whole byte must be used to carry the packet type.

***Carambolas.Net assumes that every host is exclusively bound to its end point (address and port).***

* Packets may be lost in transit or arrive in a different order than they were sent

  - A packet may be dropped if the buffer is full in any intermediary node along the path including sender and receiver, or in the presence of physical errors 
    that corrupt the data such as interference and broken links;
  - It's also possible that a packet may arrive much later than expected if it becomes retained by an intermediary node or due to changes in the routing path;
  - It may or may not be relevant for the user application if a packet ever arrives at the destination (see [QoS](#qos));
  - There must be a way to determine that a packet was intended for another process (a previous incarnation using the same endpoint) and ignore it 
    ([Session identifiers](#session-identifiers));
  - There must be a way to determine the relative position in which the packet (or each individual message) was transmitted by the sender 
    ([Sequence Numbers](#sequence-numbers)) so the receiver may deliver messages in the same order of transmission if required by the user application;

***Carambolas.Net uses [sequence numbers](#sequence-numbers) at the message level so that messages with different [QoS levels](#qos) (and even from different
[channels](#channels)) may be mixed and transmitted in the same packet.***

### Packet Structure
 
A packet is any datagram with a valid size (<= `MTU`) formatted according to the following rules.

    STM(4) PFLAGS(1) <CON | SECCON | ACC | SECACC | DAT | SECDAT | RST | SECRST>

       CON ::= SSN(4) MTU(2) MTC(1) MBW(4) CRC(4)
    SECCON ::= SSN(4) MTU(2) MTC(1) MBW(4) PUBKEY(32) CRC(4)
       ACC ::= SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) RW(2) ASSN(4) CRC(4)
    SECACC ::= SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) {RW(2)} PUBKEY(32) NONCE(8) MAC(16)
       DAT ::= SSN(4) RW(2) MSGS CRC(4)
    SECDAT ::= {RW(2) MSGS} NONCE(8) MAC(16)
       RST ::= SSN(4) CRC(4)
    SECRST ::= PUBKEY(32) NONCE(8) MAC(16)
    
      MSGS ::= MSG [MSG...]
       MSG ::= MSGFLAGS(1) <ACKACC | ACK | DUPACK | GAP | DUPGAP | SEG | FRAG>
    ACKACC ::= ATM(4)
       ACK ::= ANEXT(2) ATM(4)
    DUPACK ::= ACNT(2) ANEXT(2) ATM(4)
       GAP ::= ANEXT(2) ALAST(2) ATM(4)
    DUPGAP ::= ACNT(2) ANEXT(2) ALAST(2) ATM(4)
       SEG ::= SEQ(2) RSN(2) SEGLEN(2) PAYLOAD(N)
      FRAG ::= SEQ(2) RSN(2) SEGLEN(2) FRAGINDEX(1) FRAGLEN(2) PAYLOAD(N)

The number in parenthesis is the atom size in bytes. Angle brackets indicate multiple possibilities for an element. Square brackets denote an optional element.
Curly brackets denote an encrypted group.

- `PFLAGS`: Packet type;
- `STM`: Source Time in milliseconds mod 2<sup>32</sup> since 00:00:00.0000000 UTC, January 1, 0001, in the Gregorian calendar.
- `SSN`: Session Number used to identify the source instance;
- `MTU`: Maximum Transmission Unit in bytes supported by the source. A host may refuse a connection based on this value;
- `MTC`: Maximum Tranmission Channel (0 to 15) supported by the source. A host may refuse a connection based on this value;
- `MBW`: Maximum Bandwidth in bits per second supported by the source. Destination should not transmit data at a rate higher than this. 
          A host may refuse a connection based on this value;
- `ATM`: Acknowledged Time used to calculate `RTT`;
- `RW`: Receive window at the source. Maximum number of user data bytes that can be in flight for this peer; 
- `ASSN`: Acknowledged session number used to match the connection request and establish the session pair;
- `DSN`: Destination session number to reset;
- `CRC32C`: Computed CRC32-C (castangnoli);
- `PUBKEY`: Source public key used in the secure session. See [Encryption](#encryption);
- `NONCE`: Source nonce used to encrypt/sign. See [Encryption](#encryption);
- `MAC`: Authentication code. See [Encryption](#encryption);
- `MFLAGS`: Message type and options including channel (`CH`) when applicable. See [Message Flags](#message-flags);
- `ANEXT`: Next sequence number expected by the source;
- `ALAST`: Last sequence number of a series expected by the source (last of a gap);
- `ACNT`: Number of accumulated acknowledgements from source. Indicates the number of times (>1) that an equivalent `ACK` would have been repeated with the 
   same value of `ANEXT` (since the last `ACK` or `DupACK` was sent); 
- `SEQ`: Sequence number. See [Sequence Numbers](#sequence-numbers);
- `RSN`: Reliable sequence number. See [Sequence Numbers](#sequence-numbers);
- `SEGLEN`: Complete Segment length;
- `FRAGINDEX`: Fragment index;
- `FRAGLEN`: Fragment length;

#### Packets

There are 4 types of packets in both secure and insecure forms. 

Insecure packets are:

- [`CON`](#):
- [`ACC`](#):
- [`DAT`](#):
- [`RST`](#):

Secure packets are:

- [`SECCON`](#):
- [`SECACC`](#):
- [`SECDAT`](#):
- [`SECRST`](#):

##### CON (0x0C)

|      Byte |   0..3  |    4   |   5..8  |  9 10 |  11 | 12..15 |  16..19 |
|----------:|:-------:|:------:|:-------:|:-----:|:---:|:------:|:-------:|
|      Bits |  31..0  |  7..0  |  31..0  | 15..0 |7..0 | 31..0  |  0..31  | 
|     Field |   STM   |  0x0C  |   SSN   |  MTU  | MTC |  MBW   | CRC32C  |

##### ACC (0x0A)

|      Byte |   0..3  |    4   |   5..8  |  9 10 |  11 | 12..15 | 16..19 | 20 21 | 22..25 | 26..29 |
|----------:|:-------:|:------:|:-------:|:-----:|:---:|:------:|:------:|:-----:|:------:|:------:|
|      Bits |  31..0  |  7..0  |  31..0  | 15..0 |7..0 | 31..0  | 31..0  | 15..0 | 31..0  |  0..31 |
|     Field |   STM   |  0x0A  |   SSN   |  MTU  | MTC |  MBW   |  ATM   |  RW   |  ASSN  | CRC32C |

##### DAT (0x0D)

|      Byte |   0..3  |    4   |   5..8  |  9 10 | 11..N | N+1..N+4 | 
|----------:|:-------:|:------:|:-------:|:-----:|:-----:|:--------:|
|      Bits |  31..0  |  7..0  |  31..0  | 15..0 |       |   0..31  |
|     Field |   STM   |  0x0D  |   SSN   |   RW  |  MSGS |  CRC32C  |

##### RST (0x0F)

|      Byte |   0..3  |    4   |   5..8  |  9..12 |  
|----------:|:-------:|:------:|:-------:|:------:|
|      Bits |  31..0  |  7..0  |  31..0  |  0..31 |
|     Field |   STM   |  0x0F  |   DSN   | CRC32C |

##### SECCON (0x1C)

|      Byte |   0..3  |    4   |   5..8  |  9 10 |  11 | 12..15 | 16..47 | 48..51 |
|----------:|:-------:|:------:|:-------:|:-----:|:---:|:------:|:------:|:------:|
|      Bits |  31..0  |  7..0  |  31..0  | 15..0 |7..0 | 31..0  |        |  0..31 | 
|     Field |   STM   |  0x1C  |   SSN   |  MTU  | MTC |  MBW   | PUBKEY | CRC32C |

##### SECACC (0x1A)

|      Byte |   0..3  |    4   |   5..8  |  9 10 |  11 | 12..15 | 16..19 | 20 21 | 22..53 |  54..61 | 62..77 |
|----------:|:-------:|:------:|:-------:|:-----:|:---:|:------:|:------:|:-----:|:------:|:-------:|:------:|
|      Bits |  31..0  |  7..0  |  31..0  | 15..0 |7..0 | 31..0  | 31..0  | 15..0 |        |  63..0  |        |
|     Field |   STM   |  0x1A  |   SSN   |  MTU  | MTC |  MBW   |  ATM   |  RW*  | PUBKEY |  NONCE  |   MAC  |

<sup>* encrypted</sup>

##### SECDAT (0x1D)

|      Byte |   0..3  |    4   |  5 6  |  7..N |  N+1..N+8 | N+17..N+24 | 
|----------:|:-------:|:------:|:-----:|:-----:|:---------:|:----------:|
|      Bits |  31..0  |  7..0  | 15..0 |       |   63..0   |            |
|     Field |   STM   |  0x1D  |  RW*  | MSGS* |   NONCE   |     MAC    |

<sup>* encrypted</sup>

##### SECRST (0x1F)

|      Byte |   0..3  |    4   |  5..37 |  38..45 | 46..61 |
|----------:|:-------:|:------:|:------:|:-------:|:------:|
|      Bits |  31..0  |  7..0  |        |  63..0  |        |
|     Field |   STM   |  0x1F  | PUBKEY |  NONCE  |   MAC  |

#### Messages

Messages are encoded in the same way regardless of the packet being secure or insecure because the encryption algorithm must be format-preserving.

##### Message Flags

|      Bit   |   7    |     6     |    5    |     4     |  3..0 |
|-----------:|:------:|:---------:|:-------:|:---------:|:-----:|
|       Flag |  ACK   |  GAP/REL  |   DATA  |  DUP/FRAG |   CH  |


- All control acks are `0b1--0----`;
  - There is only one control ack currently supported that is `ACKACC = 0x8A`;
- All data acks are `0b1--1----`;
  - Bit 6 indicates if it has gap information;
  - Bit 4 indicates if it has duplicate information;
  - Bits 3 to 0 indicate the channel;
- All user data messages are `0b0--1----`;
  - Bit 6 indicates if it is unreliable(0) or reliable (1);
  - Bit 4 indicates if it is a segment(0) or fragment (1);
  - Bits 3 to 0 indicate the channel;

##### ACKACC (0x8A)

|      Byte |    0   |   1..4  | 
|----------:|:------:|:-------:|
|      Bits |  7..0  |  31..0  |
|     Field |  0x8A  |   ATM   |

##### ACK (0xA-)

|      Byte |    0   |   0  |    1 2  |   3..6  | 
|----------:|:------:|:----:|:-------:|:-------:|
|      Bits |  7..4  | 3..0 |  15..0  |  31..0  |
|     Field |  1010  |  CH  |   NEXT  |   ATM   |

##### DUPACK (0xB-)

|      Byte |    0   |   0  |    1 2  |    3 4  |   5..8  | 
|----------:|:------:|:----:|:-------:|:-------:|:-------:|
|      Bits |  7..4  | 3..0 |  15..0  |  15..0  |  31..0  |
|     Field |  1011  |  CH  |   CNT   |   NEXT  |   ATM   |

##### GAP (0xE-)

|      Byte |    0   |   0  |    1 2  |    3 4  |   5..8  | 
|----------:|:------:|:----:|:-------:|:-------:|:-------:|
|      Bits |  7..4  | 3..0 |  15..0  |  15..0  |  31..0  |
|     Field |  1110  |  CH  |   NEXT  |   LAST  |   ATM   |

##### DUPGAP (0xF-)

|      Byte |    0   |   0  |    1 2  |    3 4  |    5 6  |   7..10  | 
|----------:|:------:|:----:|:-------:|:-------:|:-------:|:--------:|
|      Bits |  7..4  | 3..0 |  15..0  |  15..0  |  15..0  |   31..0  |
|     Field |  1111  |  CH  |   CNT   |   NEXT  |   LAST  |    ATM   |

##### SEG (0x2- or 0x6-)
 
|      Byte |        0      |   0  |    1 2  |    3 4  |    5 6  |   7..SEGLEN+7  | 
|----------:|:-------------:|:----:|:-------:|:-------:|:-------:|:--------------:|
|      Bits |      7..4     | 3..0 |  15..0  |  15..0  |  15..0  |                |
|     Field | 0010 or 0110  |  CH  |   SEQ   |   RSN   |  SEGLEN |     PAYLOAD    |

##### FRAG (0x3- or 0x7-)
 
|      Byte |        0      |   0  |    1 2  |    3 4  |    5 6  |      7     |   8 9   |  10..SEGLEN+9  | 
|----------:|:-------------:|:----:|:-------:|:-------:|:-------:|:----------:|:-------:|:--------------:|
|      Bits |      7..4     | 3..0 |  15..0  |  15..0  |  15..0  |     7..0   |  15..0  |                |
|     Field | 0011 or 0111  |  CH  |   SEQ   |   RSN   |  SEGLEN |  FRAGINDEX | FRAGLEN |     PAYLOAD    |


### Connection

#### 3-Way Handshake {#three-way-handshake}


                    A                                         B
     [Disconnected] |                                         | [Disconnected]
                    |   CON {STM=44;SSN=13}                   |
       [Connecting] |---------------------------------------->| [Accepting]
                    |   ACC {STM=50;SSN=98;ATM=44;ASSN=13}    |
        [Connected] |<----------------------------------------|
                    |   ACKACC {STM=55;SSN=13;ATM=50}         |
                    |---------------------------------------->| [Connected]
                    |                                         |
                    .                                         .
                    .                                         .
                    .                                         .

- `A` transmits a connection request to `B`; 
  - `STM` provides `B` with a reference to determine later how close in time two packets from `A` are and estimate the [packet lifeTime](#packet-lifetime). It also 
     serves to calculate round-trip time at `A` when echoed back as the `ATM` field in `ACKACC`;  
  - `SSN` indicates the session number by which `A` wants to be identified; `B` can now simply drop packets from other sessions 
     (see [Session identifiers](#session-identifier)) *except for CON packets*;
  - If two or more `CON` packets arrive in sequence this may indicate that either:
    1. The same remote host is retransmitting because it hasn't received a reply;
    2. More than one process with the same endpoint is trying to connect at the same time (this is irregular and should not happen, may cause the connection to 
       be reset. Refer to [Vulnerabilities](#vulnerabilities) for more details);
    3. A previous process `A'` sent one or more `CON` packets, terminated and then another process, `A`, reusing its predecessor's end point, sent
      a `CON` packet; 

    *How in face of possible out of order delivery can `B` determine if a `CON` packet is valid?*

    In the first case the `SSN` must be the same and serve as enough evidence that it's a retranmsission; in the other two cases, the best `B` can do is rely 
    on both packets originating from the same network node (there's no way to tell if they're coming from behind a gateway at this point) and assume the 
    internal system clock used to generate `STM` is somewhat synchronized so that the latest `STM` can used to pick the latest request sent and drop the
    other; 

- `B` replies with `ACC` indicating its `SSN`, an `ATM` so that `A` can calculate the round-trip time and the remote session number `ASSN` to match so that 
  `A` can determine if the `ACC` packet received was in response to a late `CON` packet from a previous `A'` process other than its own;
- In practice `A` and `B` have to negotiate more than only session numbers. A connection must only operate under the minimum of the MTUs reported by the hosts. The 
  same applies to the maximum transmission channel. The hosts must also exchange information about the maximum receiving bandwidth each other plans to dedicate and to 
  which the corresponding remote side should adhere by not sending data in excess;

#### Cross-connection Handshake

This is a scenario where both hosts know each others end points and try to actively connect aproximately at the same time.

                    A                                         B
     [Disconnected] |                                         | [Disconnected]
                    | CON {STM=44;SSN=13}                     |
       [Connecting] |------------\        CON {STM=48;SSN=17} |
                    |             \  /------------------------| [Connecting]
                    |              \/                         |
        [Accepting] |<-------------/\                         |
                    |                \----------------------->| [Accepting]
                    |                                         |
                    | ACC {STM=52;SSN=13;                     |
                    | ATM=48;ASSN=17}     ACC {STM=65;SSN=17; |
                    |------------------\      ATM=44;ASSN=13} |
                    |                   \  /------------------|
                    |                    \/                   |
                    |<-------------------/\                   |
                    |                      \----------------->|
                    |                                         |
                    |   ACKACC {STM=75;SSN=13;ATM=65}         |
                    |---------------------------------------->| [Connected]
                    |   ACKACC {STM=78;SSN=17;ATM=52}         |
        [Connected] |<----------------------------------------|
                    .                                         .
                    .                                         .
                    .                                         .

More complicated scenarios arise when `CON`, `ACC` and `ACCACK` are retransmitted.

#### Half-open connection


                    A                                         B'
     [Disconnected] |                                         | [Connected to A'{SSN=5}]
                    |   CON {STM=44;SSN=13}                   |
       [Connecting] |---------------------------------------->| STM < latest STM received from A' so this must be,
                    |                                         | a half-open connecttion; A is the current process 
                    |                                         | incarnation; close B' and replace it with B; the 
                    |                                         | whole process is transparent to A.
                    |                                         |
                    |                                         X
                    |                                          
                    |                                         B 
                    |                                         | [Accepting] 
                    |   ACC {STM=50;SSN=98;ATM=44;ASSN=13}    |
        [Connected] |<----------------------------------------|
                    |   ACKACC {STM=55;SSN=13;ATM=50}         |
                    |---------------------------------------->| [Connected]
                    |                                         |
                    .                                         .
                    .                                         .
                    .                                         .

Note that in a secure sesion neither receiver nor sender can reliably detect all combinations of half-open connections.

A secure receiver cannot trust `SECCON` because despite the name it's not yet a secure packet and an attacker could forge a `SECCON` in order to cause the
target to reset. Refer to [Vulnerabilities](#vulnerabilities) for more information.

Another case of half-open connection may occur when the passive side suffers a reboot:


                    A'                                        B 
         [Connected |                                         | [Disconnected]
      to B'{SSN=10] |       DAT {STM=44;SSN=5}                |
       [Connecting] |---------------------------------------->| 
                    |       RST {STM=45;DSDN=5}               | 
     [Disconnected] |<----------------------------------------|
                    |                                         | 
                    |                                         |
                    X                                         X

Note that a host in a secure session cannot trust any insecure packet including `RST` for the same reasons a `SECCON` cannot be trusted and if `B` is 
disconnected it has no shared key with `A` to be able to create a `SECRST`. 

### Disconnection

[TCP/IP describes a cooperative disconnection procedure](https://tools.ietf.org/html/rfc793#section-3.5) where hosts may indicate to each other when there is 
no more data to send, a process analogous to producing an EOF marker on an output stream. SCTP, being stream oriented, follows a [similar, albeit more 
elaborate, pattern](https://tools.ietf.org/html/rfc4960#section-9.2). Other open-source reliable UDP libraries such as ENet and LiteNetLib also reproduce a 
similar same pattern, despite being message oriented protocols, so their motivations are less clear.

In TCP, disconnection handshake serves multiple purposes:

1) TCP, as a stream protocol, has no concept of message boundaries. The whole stream may be regarded as a giant message and a TCP-FIN serves to inform the 
   remote end that this giant message has been transmitted completely - this makes more sense when we think of TCP as a channel for transmitting a single 
   arbitrarily large data unit such as a file, but starts to become fuzzy when an application has to encode multiple independent data units in the stream
   (multiple files or messages forming an application protocol);

2) Contributes to reduce the number of useless packets on the network, specially unecessary retransmissions that an abrupt unilateral termination could induce;

3) Permits that unacknowledged data, sent before the TCP-FIN be retransmitted as necessary;

In practice however, TCP connection closing suffers from a few drawbacks:

1) A host may not really disconnect until the remote end has also sent its own TCP-FIN. As a result, the user application may become indefinetely stuck waiting for 
   a remote host to close. In order to remedy this situation user applications have to impose a time limit and eventually abort the connection by sending a TCP-RST;

2) A time-wait is required to ensure that the last TCP-FIN can be properly acknowledged. Time-waits cannot be reliably imposed in user-space implementations but
   the TCP/IP stack being implemented by the underlying operating system can do it. Even so, many socket implementations let users reduce or eliminate TCP 
   time-wait completely to speed up service restarts or reduce resource consumption (e.g. SO_LINGER). Deepak Nagaraj wrote a [nice article](http://deepix.github.io/2016/10/21/tcprst.html) 
   about it;

3) In most use cases, TCP/IP is not transmitting a single data unit or data units that are completely independent from feedback of the remote end. For instance,
   consider the arguably most ubiquituous client/server model. Once a server decides to disconnect there's little a client can do about it, no matter how much 
   data it forces the server to receive, everything is going to be disccarded in the applicatino level;

4) In real-life, because stream flows are a much more complex exchange than that of single and independent data units, the definition of a "graceful" disconnection
   is beyond what can be accomplished by TCP/IP alone and must often be implemented by the user application in the form of a higer level protocol on top of TCP/IP;

5) Because of (2) and because of other real-life scenarios such as half-open connections, user applications cannot avoid having to anticipate and handle aborted 
   connections even when (4) is not an issue;

Taking all into consideration, **Carambolas.Net does not try to define what a "graceful" disconnection is, leaving that to the user application.**
Therefore, no disconnection handshake is specified. Firstly, a host may become unresponsive at any time without notice so any user application must already be 
capable of handling abrupt disconnections due to timeouts. And secondly, user applications may require wildly different disconnection steps, which can be more 
efficiently implemented using custom payloads and any of the available [QoS levels](#qos). For example, a user application designed to exchange files may require
that a sender does not actively disconnect until a receiver has confirmed that the file has been completely received, but may not care about active disconnections 
happening while exchanging other types of payloads. This sort of requirement still depends on a cooperative sender but requires knowledge about the message payloads 
that is unattainable by the underlying protocol. 

A `RST` packet is specified, however, to indicate to a remote host that the connection is not available anymore, similarly to how 
[SCTP defines the ABORT chunk](https://tools.ietf.org/html/rfc4960#section-9.1) thus mitigating the problem of having to wait for a timeout and possibly saving a lot of 
garbage in the form of unecessary retransmissions. No guarantees can be given that such packet will ever arrive at its destination, though. Yet, in some cases a host 
may be able to detect a half-open connection and re-issue the `RST` packet.
 
Implementations may opt to support a local disconnecting state, for those cases when a user application wants to initiate a disconnection but still requires that 
previously sent messages be acknowledged. In this case, the local host must not accept any more data from the user application to transmit and once all buffered 
data has been transmitted and acknowledged it should send a `RST` and close.

Note that an insecure receiver is vulnerable to a form of [RESET-attack](#reset-attack) similar to the [one described for TCP/IP](https://en.wikipedia.org/wiki/TCP_reset_attack). 
Efforts to mitigate this issue in an insecure context are considered futile. If this is perceived to be a significant threat, one should consider using a secure 
session instead. This doesn't come without a cost. Secure sessions impose more overhead and are not capable of detecting some scenarios of half-open connections.
Make sure to refer to [Encryption](#encryption) and [Vulnerabilities](#vulnerabilities) for more information.

#### State Machine

[Peer](#peer) is the term used to refer to the remote host of a connection. Internally a peer may be in one of 4 private states of protocol control:

* Disconnected
* Connecting
* Accepting
* Connected

And in one of 4 public states:

* Disconnected
* Connecting
* Connected
* Disconnected

Public states are the ones perceived by a user application while internal states are those necessary to control and affect the underlying connection in 
real-time. This distinction is necessary as the user application is expected to run in a separate thread and events returned by non-blocking receive operations 
backed by message buffers may appear confusing in terms of what to expect from a peer. For instance, consider a host that receives large numbers of messages in
the form of bursts spaced out by some silent time. If immediately after one of these bursts the connection is terminated (either due to a `RST` packet or a 
timeout), what should happen?

*From one side, the connection has just been terminated and there is a peer object that must now be considered disconnected. Any send operation 
should fail. However, from the user's perspective there's a bunch of data still arriving, immediately available from the host's internal message buffer.*

Should a user application be delivered a disconnection event from a host and yet remain capable of reading the buffered data? Should the data be discarded? 
Should the event be postponed instead? If the event is postponed, does it make sense to display a peer's connection state as disconnected if no disconnection 
event has been delivered yet? 

For practical reasons, it is easiest and often more efficient to track these two states, delivery (aka public state) and internal state, separately. A public 
state is expected to reflect what has been delivered to the user-application while the internal state must be consistent with the conccurent communication 
established with a remote host and may remain concealed from the user.

This is specially convenient when we consider changes in the delivery state must always be initiated by the user-application, while changes in the internal 
state are mostly produced asynchronously by the communication thread. An implementation may thus take advantage of this characteristic to reduce lock contention. 
The only requirement is that send operations must fail silently if the internal state is disconnected but the public state has not been updated yet to reflect that. 
This in general should not be a problem because neither the protocol nor the library may offer real guaranteed delivery. Only best effort (aka eventual) delivery, 
which may be considered a form of ["at least once" weak reliability](https://en.wikipedia.org/wiki/Reliability_(computer_networking)).

The following diagram includes a virtual initial state `S` for representation purposes only, this state is never observed in practice. `R:` and `S:` indicate a 
transition's input and action respectively (received and sent packet). The term in square brackets indicate an implemenation event handled internally. The state 
machine for secure sessions is analogous.

 

                                                                        user app.    
                                                                        disconnect() 
                                                                        S:RST        
                                                 /---------------------------------------------------------\
                                                /                                                           \
                                 TIMEOUT       /                          R:RST                              \   
                                 S:CON        /                           S:-                                 \     
                                   /--\      /    /-------------------------------------------------------\    \  
                                   |  |     /    /                                                         \    \  
                                   |  |    /    /    R:ACC                R:ACC(mode=active)                \    \  
       user app. connect()         |  v   /    /     S:ACKACC             S:ACKACC                           \   |  
       S:CON                    +------------+/      [OnConnected]        /--\                               |   |  
       [OnConnecting]   /-----> | Connecting | ---------------\           |  |                               |   |  
                       /        +------------+                 \          |  v          R:RST                v   v   
                      /                |                        \       +-----------+   S:-               +--------------+
                +---+/                 | R: CON                  \----> |           |-------------------->|              |
                | S |                  | S: ACC                         | Connected |                     | Disconnected |
                +---+\                 | [OnCrossConnecting]     /----> |           |-------------------->|              |
                      \                v                        /       +-----------+   user app.         +--------------+
                       \        +------------+                 /          |  ^          disconnect()             ^
                        \-----> | Accepting  |----------------/           |  |          S:RST                    |
               R:CON            +------------+\    R:ACKACC or ACC        \--/                                   | 
               S:ACC             |  ^   |  ^   \   S: -                  R:ACKACC or ACC                         /  
               [OnAccepting]     |  |   |  |    \  [OnConnected]         S:-                                    /  
                                 |  |   |  |     \                                                             /     
                                 \--/   \--/      \-----------------------------------------------------------/
                             R:CON        TIMEOUT                        R:RST  
                             S:ACC        S:ACC                          S:-

              +-----------------------------------------+-------------------------------------+-------------------------------+  
              |                CONNECTING               |       CONNECTED/DISCONNECTING       |          DISCONNECTED         |      
              +-----------------------------------------+-------------------------------------+-------------------------------+


A user application may either disconnect immediately or disconnect after all buffered data is flushed and delivered to the connected peer. In this case,
the peer remains in a *Connected* internal state while its public state changes to `DISCONNECTING`. The user application may still cause an 
immediate disconnect at any time but is not allowed to send any new data.


### Sequence Numbers

When a message arrives, the receiver may find it relevant to determine how this message relates to other messages already received or yet to be received from 
the sender. This forms the basis for the definition of different [levels of service](#qos) (delivery services, for that matter).

More specifically a receiver would like to answer the following questions:

- Has the message arrived behind, in order or ahead of other messages?
- If behind, is it still relevant? How far behind is it (how many messages behind)?
- If ahead, can it be delivered to the user application right now or should we wait for the late ones? How many messages ahead is this one?
- Has this message been received yet (is it a duplicate/retransmission)? 

***Carambolas.Net employs two types of sequence numbers. `SEQ` is the main sequence number used to identify a message and acknowledge receipt. In order to support
both reliable and unreliable sequenced delivery on the same channel, a second sequence number is used, `RSN`, that is only incremented when a new reliable 
message is transmitted.***

With both `SEQ` and `RSN` (transmitted with every message) a receiver is thus capable of restoring the original order of transmission and determine for every message, 
specially those arriving ahead of others, if there's any prior reliable message still to be received or all eventual missing messages are unreliable.

A receiver reconstruct the original transmission order regardless of the order that messages may arrive as long as: 

  1) The initial sequence number is known by both ends; 
  2) Sequence numbers are strictly increasing;
  3) The difference between any two consecutive sequence numbers is 1;

In practice only item number 2 requires special consideration:

  - There are ways to represent arbitrarily large integer numbers (i.e. bigint) but they are computationally expensive when compared to simple fixed size integers;
  - An unsigned 64-bit integer starting from zero could be used to tag 1.8446744 * 10<sup>19</sup> messages before wraping around. This is roughly equivalent to a sender
    transmitting 1 billion messages per second for 584 years. Definitely more than enough. Unfortunately this means 8 extra bytes of overhead per message from which
    the 4 most significant will always be 0 for the first 4 billion messages or so which is starting to look like a waste;
  - There are relatively simple methods to compress low 64-bit integer values and mitigate the wasted space (i.e. varint) but eventhough they're not as expensive 
    as arbitrarily large integers they're still considerably more expensive than using regular small integers;

*What's the minimum number of bytes required for a sequence number?*

Surprisingly the answer to this question can be as low as 1 bit as long as we are willing to accept a few constraints. This problem is a major aspect of 
[Sliding Window Protocols](#sliding-window-protocols).

Consider a strictly increasing finite sequence S of 1-byte numbers starting from 0 with a constant difference of 1.

    S = ( 0, 1, ... 254, 255 )

Note that *s<sub>a</sub>* comes before *s<sub>b</sub>* if and only if *s<sub>a</sub>* < *s<sub>b</sub>*. This may seem obvious at first but now consider an 
infinite sequence Z so that for every element *z<sub>i</sub>* there is an element *s<sub>j</sub>* so that *j = i mod 256*. This is equivalent to having: 

    Z x S => ( (z0, s0); (z1, s1); ... (z254, s254); (z255, s255); (z256, s0); (z257, s1); ... (z510, s254); z(511, s255); ... )

In this sequence it's not possible anymore to determine the relative order between any two arbitrary elements just by looking at *s* but it's possible to say 
that *(z<sub>p</sub>, s<sub>a</sub>)* comes before *(z<sub>q</sub>, s<sub>b</sub>)* if q - p < 128 and 0 < (b - a)<sub>mod 256</sub> < 128. That is, although 
it's not possible anymore to order random elements it's still possible to order ZxS as long as we only have to compare elements that are at most 128 positions 
apart. Therefore we may redefine *s<sub>a</sub>* < *s<sub>b</sub>* to:

*s<sub>a</sub>* < *s<sub>b</sub>* <=> 0 < (b - a)<sub>mod 256</sub> < 128
 
This delta of 128 positions is called a *window* and can be generalized to any positive range so that for R = [0, r-1], r > 1 there is a maximum window 
W<sub>R</sub> = floor(r / 2)

{#16-bit-sequence-numbers}
By employing an unsigned 16-bit sequence number, for instance, a receiver must be able to order up to 32768 messages with an extra overhead of only 2 bytes per
message. A design decision that not only affects the packet structure but also the amount of memory a receiver may have to allocate (consider a worst case 
scenario in which all messages arrive in the reverse order!)
 
A Carambolas.Net (insecure) packet containing a single user data segment looks like this:
    
    IPHEADER(40) UDPHEADER(8) PFLAGS(1) SSN(4) RW(2) MSGFLAGS(1) SEQ(2) RSN(2) SEGLEN(2) PAYLOAD(N) CRC(4) 

Maximum N is the Maximum Segment Size (`MSS`) and it depends directly on the negotiated `MTU` for the connection.

max(`MTU`, N) = `MTU` - 40 - 8 - 1 - 4 - 2 - 2 - 2 - 2 - 4  = `MTU` - 66

max(1280, N) = 1280 - 65 = 1214
      
That means that a sender may transmit up to 32768 messages of 1214 bytes (aprox. 37MB) worth of user data until it needs to hear from the receiver again (about 
missing messages or with a confirmation to proceed to the next 32768). Assuming for simplicity that the receiver is infinitely fast, this wait time would be a minimum 
of 1 [roundtrip time (RTT)](#round-trip-time), or in other words, the amount of time that all the packets would take to arrive at the receiver plus the time
a reply takes to travel from the receiver back to the sender. It's generally safe to assume `RTT` > 0.001s although in real networks this value must be one or
two orders of magnitude higher. This amounts to a purely hypothetical (and totally unrealistic) upper limit for the throughput of aprox 37GB/s (or about 296Gbit/s). 
Nevertheless, even with an `RTT` of 0.15s, which is much more likely to occur, we would still reach aproximately 252MB/s (or about 2Gbit/s) - way more than any
user applicaton is expected to push into the link (see [Normal operating conditions](#normal-operating-conditions)).


##### Comparing unsigned n-bit sequence numbers

Given two unsigned n-bit sequence numbers (*s*, *t*) we say *s* < *t* if 0 < (*t* - *s*) < 2<sup>n-1</sup>, computed in unsigned n-bit arithmetic. This means
that if *s* is within a distance from *t* (in modulo 2<sup>n</sup>) that is greater than or equal to 2<sup>n-1</sup> we must assume it's from a previous "window", thus lower 
than *t* in respect to order.


### Time source

Carambolas.Net requires a monotonic non-decreasing 32-bit unsigned time counter capable of returning the time in milliseconds modulo 2<sup>32</sup> since 
`EPOCH` with granularity `G` <= 1ms.

`EPOCH` is 00:00:00.0000000 UTC, January 1, 0001, in the Gregorian calendar.

The time source is used to stamp packets (`STM`) and generate session identifiers (`SSN`). This means that even if a host were capable of producing a packet 
every millisecond it would still take aprox. 49 days for an `STM` collision. The same is also applicable to `SSN` generation. No packet is expected to remain 
in flight for such a long time so, in a sense, `STM` serves as a form of high level sequence number that can be used to detect [old packets](#old-duplicates) and 
[retransmissions](#retransmissions). 

A unsigned 32-bit timestamp can be viewed as a particular case of an [n-bit sequence number](#comparing-unsigned-n-bit-sequence-numbers) with a maximum window
size of 2<sup>31</sup> milliseconds, that is roughly equivalent to 24 days. 

Since Carambolas.Net is a user-space protocol, host instances might be spread over multiple processes. In this case, there's no reliable way to employ a global
time-wait strategy (as employed by TCP) in order to address the problem of old packets arriving from a previous connection. Thus, a time source must be resilient
to system crashes and reboots or external adjustments performed during runtime (i.e. daylight saving time or user settings). 

Generally, the system clock cannot offer these guarantees, but there is often an alternative in the form of a secondary general purpose monotonic non-decreasing 
counter (e.g. clock ticks since system startup, proccess time, etc). If a platform can offer such a counter with at least 1KHz, the following procedure may be 
used: 

  - On startup: 
    - Let `UTCSCR` be a time source that returns the current time in UTC with granularity `G` = 1ms;
    - Let `TICKSRC` be a general purpose monotonic non-decreasing counter;
    - Let `FREQ` be the frequency in ticks/s of `TICKSRC`;
    - Store `START`<sub>time</sub> = |`UTC` - `EPOCH`| in milliseconds modulo 2<sup>32</sup>;
    - Store `START`<sub>ticks</sub> = `TICKSRC`
  - On time requested:
    - Return [`START`<sub>time</sub> + (`TICKSRC` - `START`<sub>ticks</sub>) / (`FREQ` * 1000)] mod 2<sup>32</sup> 

Note that the time sources used by any two hosts in a connection never depend on each other, so they are not required to be synchronized. Clock skew, however,
should be kept to a minimum, preferably below 10%, as it will directly impact `RTT` estimation on each respective remote end.


### Roundtrip time

Roundtrip time (`RTT`) is effectively the amount of time that it takes for a transmitted message to arrive at the destination plus the time taken for a 
confirmation to be replied back and arrive at the sender. 

[Acknowledgements](#acknowledgements) which are already used as a form of confirmation of receipt can be used to measure `RTT` if they are modified to carry 
the latest `STM` received since the last `ACK` (or variant) was transmitted. This time information is then refered to as acknowledged time (`ATM`).

The problem with roundtrip time is that it's the result of propagation delay and processing delays of intermediary nodes in the network path to the destination
and back. There's no guarantee that two consecutive measurements will return the same value. In fact, any `RTT` measured is by definition out-of-date because 
it represents the time already taken by a message (and back). Nothing can be said, about what the next message will subject to. In fact, two consecutive 
measurements may return wildly different results in face of eventual route changes, packet loss, varying performance of intermediary nodes, etc. So since we 
cannot assume `RTT` to remain constant for any arbitrarily small time interval we must devise a method to produce a reasonable estimate.

This problem has been extensively researched in the early days of the internet and the proposed method is the same specified by [TCP](https://tools.ietf.org/html/rfc6298#section-2):

- `SRTT` (smooth roundtrip time) is the currently estimated `RTT`;
- `RTTvar` is the currently estimated `RTT` variance;
- When the first `RTT` measurement *R* is made:
  - `SRTT` = max(*R*, 0.001)
  - `RTTvar` = *R*/2
- When a subsequent `RTT` measurement *R'* is made:
  - `RTTvar` = 3/4 * `RTTvar` + 1/4 * |`SRTT` - *R'*|
  - `SRTT` = max(7/8 * `SRTT` + 1/8 * *R'*, 0.001)


### Sliding Window Control

***Carambolas.Net employs a modified version of the the sliding window protocol where unreliable messages that timeout may be dropped. Therefore despite a 
window size of 32768, only 32767 messages may carry user data. The last message of the window is reserved for an eventual [ping](#ping) in case all the previous
messages in the window are dropped.*** 

Consider the case where a sender must transmit an arbitrarily large number of messages. Each message must be marked with a 16-bit `SEQ` but no more than 32768 
messages may be transmitted in a row before the sender needs to wait for a reply back with some kind of confirmation that either everything has been received or 
that some message is missing.

In theory, a sender could be implemented in a way that a new message would only be transmitted after a confirmation (from the receiver) that a previous one had 
been successfully received. In practice, however, the time that it takes for an acknowledgement to travel back may be significant. It's often going to be 
comparable (if not similar) to the time the initial message took to arrive at the destination. In this case, the resulting throughput is bound to be much lower
(possibly half) than what could be achieved (theoretically). And the perceived latency will be proportional to the total number of messages in the output queue. 
For instance, a sequence of 10 messages *m<sub>0</sub>* to *m<sub>9</sub>* will take 10 * `RTT` seconds to be fully transmitted regardless of the actual link 
bandwidth.

If all messages are equally important and must eventually be delivered, a [sliding window protocol](#sliding-window-protocols) can be employed. The maximum number
of messages allowed to be in flight (that is, messages that have been transmitted but not acknowledged yet) is the size of the sequence window (32768). Sender and 
receiver must then be implemented as follows:

**Sender:**

*The sender must "push" its sliding window forward.* 

The sliding window of the sender is the range of sequence numbers that can be used to transmit new messages. Every time a valid acknowledgement (`ACK`) is 
received, the upper bound of the window is recalculated in terms of the next sequence number expected by the receiver (`ACK`.`ANEXT`). A sender must not 
transmit new messages beyond the upper bound of its sliding window for such messages would either be dropped or misinterpreted by the receiver.

Given a sender that tracks:

- `TX.SEQ`: the next sequence number to send;
- `TX.ASEQ`: the next sequence number expected by the receiver;

The invariants are:

1) `TX.SEQ` >= `TX.ASEQ`;
2) |`TX.SEQ` - `TX.ASEQ`| < 32768;

By combining (1) and (2) we can derive that the next sequence number that can be transmitted by a sender must be either the next expected by the receiver or a 
value inside the sequence window of 32768 elements from the next expected (that is `TX.ASEQ` <= `TX.SEQ` <= `TX.ASEQ` +  32767). Every valid `ACK` will 
cause `TX.ASEQ` to move forward, thus making room for more messages to be transmitted.

Note that this is an oversimplification to illustrate the main point. In practice, acknowledgements may arrive out of order, too late or not arrive at all.
In order to communicate anomalies to the sender a receiver may issue alternative acknowledgements with more information than just a `SEQ`. 
Refer to [Acknowledgements](#acknowledgements) for more details. In the same context, a sender has to keep track of [retransmissions](#retransmissions). 


**Receiver:**

*The receiver must "pull" its sliding window forward.* 

The sliding window of the receiver is the range of sequence numbers that must be acknowledged to the sender. It can be further divided in two subranges:
The lower subrange is comprised of sequence numbers that have been received already but to which the sender might not have received an `ACK` yet. It's expected
that duplicates of these messages may still arrive (due to retransmissions) and they must be acknowledged all the same, though not used any further. The upper 
subrange is comprised of sequence numbers that have not been received yet, that is, of expectedly new messages (effectively the full range of 32768 messages 
from and including the next sequence number expected, which is equivalent to any sequence number *s* >= the next expected). The division point of the two 
subranges is thus the next sequence number expected (`RX.SEQ`).

Given a sender that tracks:

- `RX.SEQ`: the next sequence number expected;
- `RX.LASEQ`: the lowest acknowledgeable sequence number;

The invariants are:

1) `RX.LASEQ` <= `RX.SEQ`;
2) |`RX.SEQ` - `RX.LASEQ`| <= 32768;

When a message *m* arrives:

```
if (m.SEQ >= RX.LASEQ)
{
    send ACK

    if (m.SEQ == RX.SEQ) // m is the next expected       
    {
        do 
        {
            deliver m       
            RX.SEQ++
            m = next buffered message
        }
        while (m.SEQ == RX.SEQ);        
    }
    else if (m.SEQ > RX.SEQ)  // m is ahead of the next expected
    {
        if (m.SEQ has not been received yet)
            buffer m
    }

    // Adjust the lower bound of the sliding window according to RX.SEQ, "pulling " it forward
    if (RX.LASEQ < RX.SEQ - 32768)
        RX.LASEQ = RX.SEQ - 32768
}
```

### Acknowledgements

An acknowledgement `ACK` is a control message that can be batched in a data packet but does not carry any user data. Instead, an `ACK` carries the sequence 
number of the message being acknowledged (i.e. the next sequence number expected by the receiver)

***Carambolas.Net employs cummulative acks and defines 4 alternative forms of ack with extended information.***

The convention of tracking and replying back with the next sequence number **expected** instead of the actual sequence number received/sent may look like a 
technicality but it is indeed more convenient in many ways. *Mainly by providing an initial state that is equivalent to any other intermediary state in both sender 
and receiver.* In other words, when a host starts, no message has been received or transmitted yet. An initial next sequence number expected (or to be 
transmitted) can be simply assumed zero or whatever pre-defined inital value that is more convenient. On the other hand, handling a non-existent last 
received/transmitted sequence number to calculate the next one requires special treatment in the code. Comparisons, for instance, can be simplified when a 
receiver tracks the *next* sequence number expected. If the *last* received sequence number is tracked, instead, the receiver must perform an extra operation 
for every message *m* that arrvives, that is to calculate `RX.LastSEQ` + 1 to compare with *m*.`SEQ`.
   
***Carambolas.Net, as any other [sliding window protocol](#sliding-window-protocols), requires that at least some of the messages transmitted in a sequence window 
are acknowledged (specifically the lower bound ones) before the window may advance and new messages can be transmitted.***

If a sender transmits several (possibly all) messages in a sequence window in order to maximize throughput, it can become quite expensive to reply back with an 
individual `ACK` for every message (potentially up to 32768 `ACK`s every `RTT`). Instead, sender and receiver mmust exchange cumulative `ACK`s. Instead of 
representing the acknowledgement of an individual message, an `ACK` is assumed to represent *the acknowledgement of an individual message **and** every other 
message prior to that one*. This way, a receiver can reduce the number of `ACK`s sent back after receiving several messages in a row.

Yet packets (and their containing messages) may still arrive out of order or get lost in transit, which means a receiver may end up with one or more gaps in the 
expected sequence window. For example, consider a sender that just transmitted a full window M = { *m<sub>0</sub>, m<sub>1</sub>, m<sub>2</sub>, ..., 
m<sub>32767</sub>* } of messages and is now waiting for a cumulative `ACK`. Hardly, all these messages could have fit in a single packet, so there's always a 
chance of packet loss or out of order arrival.

Consider a receiver that has just received the first few packets with messages *m<sub>0</sub>* to *m<sub>10</sub>*. Because *m<sub>0</sub>* was the next expected 
message, the receiver can deliver it, process the rest, and deliver each one to the user application in order. A cumulative `ACK` must be sent back now with 
`ACK`.`ANEXT` = *m<sub>11</sub>*,  indicating that the next expected sequence number is that of message *m<sub>11</sub>*. When the sender receives this `ACK` 
it will advance its sliding window by 11 positions and could eventually send 11 more new messages, but let's assume, for simplicity, that the sender has nothing
else to send. A few more packets, then, arrive at the receiver with messages *m<sub>12</sub>* to *m<sub>40</sub>*, *m<sub>50</sub>* to *m<sub>100</sub>* and 
*m<sub>102</sub>* to *m<sub>200</sub>*. Now the receiver, which was still expecting message *m<sub>11</sub>*, processed instead *m<sub>12</sub>* to *m<sub>40</sub>*. 
Since *m<sub>11</sub>* is still missing it cannot send a cumulative `ACK` back to the sender to indicate that *m<sub>12</sub>* to *m<sub>40</sub>*  have been 
received. The best it can do is send a duplicate `ACK`.`ANEXT` = *m<sub>11</sub>* to advice the sender that something has been received but *m<sub>11</sub>* is 
still missing. When the receiver processes *m<sub>50</sub>* to *m<sub>100</sub>*, once again it cannot send a new cumulative `ACK` as there's even a much larger 
gap now (of 41 to 49) but still it can send another duplicate `ACK`.`ANEXT` = *m<sub>11</sub>* to indicate that something more has been received but 
*m<sub>11</sub>* is still missing. In a large range such as a full 32768 window, several gaps may appear (up to 16384 actually). Some may even get fixed by 
themselves as certain packets arriving out of relative order may end up closing some gaps further away in the sequence without the sender ever becoming aware of 
those gaps. But the point is that after processing all the packets received so far, a host could avoid having to send back many repeated duplicate `ACK`s and 
in place send a single `DupACK` message that contained a counter indicating how many times an `ACK` would have been repeated with the same next sequence number 
expected. An simple `ACK` then may be deemed a special case of `DupACK` with an implicit count of 1.
 
Now let's say that the packet with message *m<sub>11</sub>* arrives. The receiver must send a cumulative `ACK`.`Anext` = *m<sub>41</sub>*. But this 
acknowledgement is now ambiguous. A sender cannot say anymore if just *m<sub>41</sub>* is missing, if everything after and including *m<sub>41</sub>* is missing 
or only some messages after *m<sub>41</sub>* are missing. It would be too complicated to construct an acknowledgement describing every gap so far in a received 
sequence but a receiver can at least indicate the immediate next `GAP` by sending back not only the next sequence number expected but also the last sequence 
number expected (`ALAST`). From the example, those are *m<sub>41</sub>* and *m<sub>49</sub>*.

By definition, `ALAST` > `ANEXT` otherwise a simple `ACK` would have sufficed. 

`GAP` is somewhat analogous to the [Gap Ack Block in SCTP](https://tools.ietf.org/html/rfc4960#section-3.3.4).

It's easy to see how the same `GAP` would have to be transmitted several times when only messages after that interval are received, like a cumulatve `ACK` would. 
Hence, we can define a `DupGAP`, similarly to a `DupACK`.

By defining 4 specialized types of acknowledgements (`ACK`, `DupACK`, `GAP`, and `DupGAP`) instead of a simple cumulative `ACK`, a sender has more information 
available to implement better [retransmission](#retransmission) strategies. Also note that, acknowledgements have been described as messages, as if a packet could
contain more than one. In fact, this seems odd when thinking about acknowledgments being cumulative over a single sequence of messages (aka a stream in SCTP) but 
it's more convenient to think this way because on connections that support multiple [channels](#channels) a packet may indeed batch multiple independent cumulative 
`ACK`s, potentially one for each supported channel, up to a total of 16.


### Ping

*A ping is an empty segment message transmitted in order to induce an acknowledgement* which provides both a confirmation that the remote host is still alive and 
listening and an oportunity to update the [estimated `RTT`](#roundtrip-time). A sender may send a ping when there's uncertainty about a remote host's capability 
to receive new messages but there's no user data to transmit. 

Not to be confused with the [ping command](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/ping)
available in the command line interface of many platforms, which is a tool used to measure roundtrip time to arbitrary destinations.

A host must ensure that the remote end remains connected and listening even in the absense of user data to transmit. In this case, a ping guarantees that 
acknowledgements will keep flowing. Once there's any user data to be acknowledged, there's no point in sending pings anymore. The maximum time a host can wait for 
some user data to transmit before sending a ping is called the `Idle Timeout`

### Session identifiers

A session identifier `SSN` is used to determine if a packet belongs to the current connection or is an old packet from a previous connection between the same 
two end points. Each host generates and advertises its own `SSN` in the connection handshake, which effectively means "the session number that is going to be 
used by the source to transmit packets". Session numbers must be defined by the source rather than the destination so that `RST` packets may be constructed by 
a receiver (echoing back the `SSN`) in the abscence of a proper connection (e.g. half-open connections).

**Carambolas.Net assumes that a packet may remain up to 24 hours in flight**. 

This is definitely an overestimation. In real life, packets are not expected to live for more than a few minutes, but it's not impossible (although extremely 
unlikely) that a network node may end up retaining a packet for even a few hours. Hence, to stay on the safe side, the `SSN` generation period should be no 
shorter than 86400000 (the total number of milliseconds in a day). In this case, a value from the host's internal [time source](#time-source) should be enough 
and cheaper than relying on more elaborate methods like a random number generator. 

**Carambolas.Net assumes that since `RTT` >= 0.001s a host must take more than 1ms to connect, close and reconnect with the same remote end point**. 

Note that we're not concerning ourselves about mitigating security threats yet. All packets are transmitted in the clear anyway and trying to mitigate the risk
of tampering or particular threats such as flooding or man-in-the-middle attacks (as attempted by TCP and SCTP) are considered futile half-measures that add a 
disproportional amount of complexity for very little to no real security benefits. Refer to [Encryption](#encryption) and [Vulnerabilities](#vulnerabilities) 
for more on this topic.

### Old duplicates

Old duplicates are packets transmitted in the past which are not relevant anymore to the receiver but may be mistaken for an up-to-date packet.

There are two main sources of old duplicates:

1) packets originated on a previous connection between the same two end points;
2) packets originated on the current connection (in a previous incarnation of the same sliding window position) that arrive late enough (possibly due to 
   retransmissions);

In the abscence of a way to tie a message (or all messages in a packet, for that matter) to a connection, the message sequence number becomes the only identifying 
resource available. As [noted before](#sequence-numbers), n-bit sequence numbers are subject to wrap around and thus can only be reliably ordered within a limited 
window of 2<sup>n-1</sup> elements. Consequently, an old packet may arrive as in (1) or (2) and contain one (or more) messages with seemingly valid sequence numbers 
for the current state of the connection - i.e. sequence numbers that are within 2<sup>n-1</sup> from the next expected value. Such late messages would be 
indistinguishable from legitimate ones at the receiver. 

Both TCP and SCTP employ strategies to address (1) and (2). TCP imposes a time-wait to reduce the probability of (1). It also uses 32-bit sequence numbers with
varying initial values so that according to [RFC793](https://tools.ietf.org/html/rfc793#page-27):
 
>When new connections are created, an initial sequence number (ISN) generator is employed which selects a new 32 bit ISN. The generator is bound to a (possibly 
>fictitious) 32 bit clock whose low order bit is incremented roughly every 4 microseconds. Thus, the ISN cycles approximately every 4.55 hours. Since we assume 
>that segments will stay in the network no more than the Maximum Segment Lifetime (MSL) and that the MSL is less than 4.55 hours we can reasonably assume that 
>ISN’s will be unique.
  
Unfortunately, for TCP, despite the assummed uniqueness of ISN's even being reasonable it's not enough to avoid wrap around in just a few roundtrips on large 
bandwidth links. Thus, in order to address (2), a receiver must employ a [special algorithm](https://tools.ietf.org/html/rfc7323#section-5) which relies on 32-bit 
timestamps. SCTP packets may include both a 32-bit timestamp and a random 32-bit Tag Value used to correlate a packet to its connection (so it can avoid imposing 
a time-wait). 

{#source-time}
Carambolas.Net requires a solution similar to that of SCTP in order to avoid the need for a global time-wait (which would be unfeasable for a user-space 
implementation anyway). Packets must be transmitted with a source timestamp (`STM`) and a session identifier (`SSN`). The value of `STM` must be assigned on 
packet transmission from the host's internal [time source](#time-source). 

There should be no need to worry about the possibility of two packets bearing the same `STM` value. An `STM` is not a unique id. In fact, it's perfectly valid 
to expect multiple packets to be transmitted with the same `STM`. If, for instance, a sender has more messages to be batched than can fit in a single packet
(due to MTU constraints), several packets could be generated and transmitted in the same iteration and this iteration could be shorter than 1ms resulting in 
those packets sharing the same `STM`. For all that matters, this should have the same practical effect as if the sender had transmitted a single arbitrarily
large packet containing all those messages. In such case, none of the messages could have been duplicates themselves. Even a retransmitted message would have been 
a minimum time apart from its previous transmission (at least one `RTT` >= 0.001s but way more than this in practice). 

**Carambolas.Net assumes that `RTT` >= 0.001s**

The `SSN` is generated in the connection handshake and the only requirement is a minimum period to avoid accidental collisions if several connections are 
estabilished and closed between the same two end points in rapid succesion. Refer to [Session identifiers](#session-identifiers) for a complete description.

With every packet containing an `STM`, and a wrap around period that is much larger than the maximum time a packet is ever expected to be in flight, we can 
track the latest `STM` received to determine how relatively late a packet is arriving. If a packet happens to be the latest or is relatively recent, it may 
contain brand new messages or valid retransmissions. If it's old beyond a [certain threshold](#packet-lifetime), the packet is considered irrelevant and 
silently dropped. 

With different levels of [QoS](#qos) in the same channel a pure [Sliding Window Control](#sliding-window-control) is not enough to validate messages and a more 
elaborate [algorithm](#time-windows) based on `STM` will be required to detect old duplicates, similarly to how 
[PAWS](https://tools.ietf.org/html/rfc1323#section-4) operates in TCP.


### Packet Lifetime

Packets older than 60s (or any other arbitrary duration) are most probably irrelevant and should be dropped, but a receiver cannot tell how much time a packet 
has been in flight because the time source used to stamp `STM` is only known by the sender. There are no guarantees that both sender and receiver will have 
synchronized time sources. 

However we can approach this problem in a slightly different way by taking into account that:

  1) `STM` is monotonically non-decreasing;
  2) If all packets arrive in order it doesn't really matter how old they are at least in the protocol level;
  3) If a packet arrives late, its time in flight must have been at least the *Δt* between its `STM` and the most recent `STM` already received;
  
So a receiver may produce an acceptable lifetime validation by keeping track of the most recent `STM` received and comparing other arriving packets to it to 
determine how relatively late they are. If a packet is instead ahead of time,`the most recent `STM` is simply updated.


### Time Windows

With a 16-bit sequence number the size of the sliding window must be at most 32768. The sliding window size also determines the maximum number of messages that 
may be in flight, in this case 32768 - 1. The reason for the -1 is that if a full window of 32768 consecutive unreliable messages are lost in a row, the sender 
is forced to move its sliding window forward by the same amount (because there's no hope of any acks arriving anymore) while the receiver's slindig window will 
remain unchanged. This will cause the receiver to misinterpret all further 32768 messages as old/late and acknowledge them all without delivering any data. The 
sender will believe these new 32768 messages are being delivered when in fact they're being acknowledged and dropped by the receiver. An easy way to address this 
problem without increasing the sequence number space is to rely on the fact that if the sender considers all messages in flight to be lost (i.e they were all 
unreliable) a ping must be injected to find if the receiver is still alive. In this case we must reserve the last sequence number of the sliding window for an 
eventual (reliable) ping message. There will be no crossing over sequence window boundaries anymore. The receiver will be able to naturally adjust its sliding 
window once the ping is received and the sender, on its side, can now safely wait for a cumulative `ACK` to move its own sliding window instead of having to 
artifically adjust when messages are lost.

A problem with sliding windows protocols is how to determine whether an incoming message is an old duplicate from a previous cycle of the window without having 
to augment the space of sequence numbers. 

Given a receiver that stores the lowest sequence number that can be acknowledged (`LSEQ`) the next expected sequence number (`ESEQ`) and the next expected 
source time (`ESTM`), any incoming message m is:

- acceptable if m.`STM` >= `ESTM`
- may be acknowledged if m.`SEQ` >= `LSEQ` 
- may be enqueued for delivery if m.`SEQ` >= `ESEQ`

Initial states are:

- `ESEQ` = 0
- `LSEQ` = 0
- `ESTM` = connection time

On message *m* delivered:

     ESEQ = m.SEQ + 1
     if (ESEQ - Protocol.Window.Size > LSEQ) 
         LSEQ = ESEQ - Protocol.Window.Size;

Without loss of generality, imagine a sender that always sends a full window of messages. In this case, after a few seconds the receiver will observe
something like the following pattern:

        [0]          [1]          [2]          [3]          [4]
     0..32767 | 32768..65535 | 0..32767 | 32768..65535 | 0..32767 | ...

It's easy to verify that `STM` is not monotonically increasing inside each window due to retransmissions. For instance m[0][0] may arrive with an 
`STM` = t0, m[0][3] and m[0][4] with an `STM` = t10 and yet m[0][1] may arrive late after being retransmitted with an `STM` = t50.

In fact this means that not all `STM`s from a window[k] are greater than or equal to those of window[k-1] (k > 0). For instance, messages m[0][32766]
to m[1][32779] may arrive with `STM` = t100, m[1][32780] with `STM` = 110 and m[0][32767] may arrive late after being retransmitted with `STM` = t150.
In this situation some messages in window[1] will have lower `STM`s than others in window[0].

Nevertheless, messages from window[2] are guaranteed to only be transmitted after all messages from window[0] (by a well-behaving sender) and that 
includes retransmissions. In the worst case, assuming m[0][32767] is reliable and continously retransmitted without reaching the receiver, even if 
all further messages in the sliding window are transmitted, the last one transmitted before a sender must stall is going to be m[1][65533] (because 
m[1][65534] would have been a ping but it's not transmitted since m[0][32767] is already acting like one - i.e. waiting for an ack). 

Therefore:

     min { m[k][i].STM } > max { m[k-2][i].STM }, k >= 2

This means that a message m belongs to window[2] if and only if m.STM is greater than the maximum STM received for window[0]. This is great because
the only requirement now is to compute the max STM of every static window as we go in a rolling buffer of 4 ESTMs like this:

     ESTM[-2] = connection time
     ESTM[-1] = connection time

                                         / m[0][i].STM >= ESTM[-2]
                                         | ESTM[0] = max { m[0][i].STM }
     while ESEQ-1 < 32768               <                                    --> no message from window[1] has been delivered yet
                                         | m[1][i].STM >= ESTM[-1]
                                         \ ESTM[1] = max { m[1][i].STM }
                                        
                                         / m[1][i].STM >= ESTM[-1]
                                         | ESTM[1] = max { m[1][i].STM }
     while ESEQ-1 < 0                   <                                    --> no message from window[2] has been delivered yet 
                                         | m[2][i].STM >= ESTM[0]
                                         \ ESTM[2] = max { m[2][i].STM }
                                        
                                         / m[2][i].STM >= ESTM[0]
                                         | ESTM[2] = max { m[2][i].STM }
     while ESEQ-1 < 32768               <                                    --> no message from window[3] has been delivered yet
                                         | m[3][i].STM >= ESTM[1]
                                         \ ESTM[3] = max { m[3][i].STM }
                                        
                                         / m[3][i].STM >= ESTM[1]
                                         | ESTM[3] = max { m[3][i].STM }
     while ESEQ-1 < 0                   <                                    --> no message from window[4] has been delivered yet 
                                         | m[4][i].STM >= ESTM[2]
                                         \ ESTM[4] = max { m[4][i].STM }
         ...                            
                                         / m[n-2][i].STM >= ESTM[n-4]
                                         | ESTM[n-2] = max { m[n-2][i].STM }
     while ESEQ-1 < 32768 * ((n-1) % 2) <                                    --> no message from window[n] has been delivered yet 
                                         | m[n-1][i].STM >= ESTM[n-3]
                                         \ ESTM[n-1] = max { m[n-1][i].STM }

     where n > 0 is the number of windows (not to be confused with the index of the last window)

Note that the actual implementation has to adjust `ESTM` indexes because we can't have negative array indexes in C# and even if we could the semantics 
would probably be different than the one we imply here (e.g. like in python) So in practice we have to calculate window index j and offset the ESTM 
index by 2 (simply assuming k = j). This means that for window 0 we test against ESTM[0] while updating ESTM[2], for window 1 we test agains ESTM[1] 
while updating ESTM[3], for window 2 we test against ESTM[2] and update ESTM[0], etc...

    ESTM[0] = ESTM[1] = ESTM[2] = ESTM[3] = latest source time on connected
    XSEQ = 0
    j = 0

On ESEQ changed:
    
    XSEQ = (XSEQ + new ESEQ - old ESEQ) % (4 * Protocol.Ordinal.Window.Size)
    j = XSEQ / 32768


Finally, a misbehaving sender that transmits messages without observing the sequence window limits would still need two full windows - i.e. the 
whole sequence number space - of messages to arrive later than some message m[k][i] for its `SEQ` to overlap with some window[k-2] message m[k-2][i] 
and possibly replace it in error.

A misbehaving sender that does not honour the monotonic increasing modulo 2<sup>16</sup> requirement of sequence numbers is out of scope.


### Fragmentation

***Carambolas.Net supports payloads of up to 65535 bytes which can be split in up to 256 fragments depending on the value of `MTU`.***

Initially, the maximum amount of data that can be transmitted (the maximum segment size `MSS`) is limited by the negotiated `MTU`. A problem may arise 
if the `MTU` advertised by the remote host is lower than the one expected/required by the user application. 

For instance, consider a user application that is required to transmit small files of 1024 bytes (1KB) exactly. This is fine if the application can assume 
`MTU` = 1280 which by the protocl spec would leave us with `MSS` = 1214 bytes. However, if the remote host requires a lower `MTU`, because it has 
additional information about the link from its end, and the resulting `MSS` < 1024 then the user application is left with 3 options:
  
  1) Disconnect;
  2) Let every send operation fail because the amount of data does not fit in the calculated `MSS`;
  3) Split each file in two or more pieces depending on the calculated `MSS` and require the remote host to be capable of re-assembling those pieces;
    
The first two options are obviously undesirable. Option 3 deserves some consideration. It implies that every user application will be required to handle
the possibility of user data being larger than the calculated `MSS` and implement a custom solution for fragmentation and reassembling. This is due to the
exact negotiated `MTU` for the connection being unpredictable. It would be ideal if a host could take care of user data fragmentation by itself. The IP layer
provides transparent datagram fragmentation for free but only at the cost of [a few additional problems](#ip-level-fragmentation) and it can only operate in the 
whole packet not at the message level. A custom strategy, on the other hand, supported by the protocol, would have a few advantages such as:

  1) Support for different QoS levels;
  2) Fine-grained retransmissions (at the fragment level rather than the whole packet);
  3) Better control over the memory allocated to hold fragments at the receiver;
  4) Better average packet occupation when transmitting the last fragment (since it can be batched with other messages in the same packet);

*How should data be fragmented?*

This problem can be further decomposed in the following questions:

- Should fragments have a variable length or should a fragment's payload always be maximal? 
- How can a receiver determine that all fragments have been received?
- How can fragments be ordered?
- What's the maximum number of fragments that can be produced?

And depending on the answers, not only will a fragment message look differently but also both sender and receiver will be faced with additional requirements. 

* If fragments are allowed to have a variable length, let's say depending on the available space in the packet (which might have been partially filled with 
  other messages) by the time of the transmission, then fragments cannot be constructed until a packet is about to be transmitted; 
  * A sender will never be able determine the number of messages waiting to be transmitted (only the total amount of user data bytes);
* If fragments are always maximal, they may all be pre-calculated. All will have the same Maximum Fragment Size (`MFS`) except for the last one that is 
  going to be `SEGLEN` % `MFS` where `SEGLEN` is the complete user data segment length;
* For a receiver to be able to determine when all fragments have been received it must know either the complete segment length or the total number of fragments
  to expect; 
  * Knowing the complete segment length has the advantage of allowing the receiver to pre-allocate all the memory needed to reconstruct the packet;
* Fragments must be ordered to form a complete segment; 
  * Each fragment message must have some kind of sequence number of its own such as a fragment index or rely on the message sequence number [as used by data segments](#16-bit-sequence-numbers); 
  * Although the idea of relying on the message sequence number to order fragments may seem attractive (specially as it does not incur extra overhead) it's 
    proved to be problematic when messages arrive out of order. Consider the case of a transmitted subsequence of messages *m<sub>0</sub>, m<sub>1</sub>, m<sub>2</sub>, m<sub>3</sub>, ... m<sub>9</sub>*. 
    At a given point in time, messages *m<sub>4</sub>* to *m<sub>7</sub>* arrive at the receiver ahead of *m<sub>0</sub>* to *m<sub>3</sub>* while *m<sub>8</sub>* to *m<sub>9</sub>* 
    have not arrived yet. The receiver can determine that the messages received so far are fragments by their `MSGFLAGS`, it can even deduce their relative 
    order from their sequence numbers and that there are still 3 more fragments to come (assumed every fragment contains either information about the complete
    segment length or the total number of fragments) but the receiver is incapable of deducing by the sequence numbers alone if the missing fragments must come before 
    *m<sub>4</sub>* or after *m<sub>7</sub>* because a message sequence number does not carry information about how a fragment must be positioned inside its 
    complete segment, it only tells how a message must be position in the big picture, that is among other messages, regardless of type;
* The maximum number of fragments that can be produced (or the correlated maximum complete segment size) will directly impact the receiver which must be able to buffer 
  at least a complete segment minus 1 byte (with 1 byte being the minimum possible size of a last fragment) 
  * Consider the worst case where all fragments arrive in the reverse order; 
  * There is no point in establishing an inpractical maximum such as 2<sup>64</sup> fragments. A maximum that cannot be honored is equivalent to letting the receiver impose any 
    arbitrary and potentially unpredicatble limit according to its own available resources (i.e. memory);

Consider a user application that is required to send files of exactly 65535 bytes. Coincidently this is also the (never-achievable) maximum UDP datagram size. 
A minimum fragment message capable of carrying fragments of a complete segment whose length is at most 65535 needs only 3 extra pieces of information when compared to 
a normal segment message:

  1) `MSGFLAGS` (1 byte) indicating if the parameters that follow are of a segment or a fragment;
  2) `SEGLEN` (2 bytes) for the complete segment length;
  3) `FRAGINDEX` (2 bytes) for the fragment index (as we may send up to 65535 fragments of 1 byte);
  
That last statement about `FRAGINDEX` sounds pretty unreasonable, though. An `MTU` that is so low as to cause `MFS` to be 1 byte should never happen in real-life.
In fact, if we can ensure `MFS` >= 256 then `FRAGINDEX` can be reduced to 1 byte (65535 = 256 * 256 - 1). An insecure packet containing a single fragment would 
look like this:

    IPHEADER(40) UDPHEADER(8) PFLAGS(1) SSN(4) RW(2) MSGFLAGS(1) SEQ(2) RSN(2) SEGLEN(2) FRAGINDEX(1) FRAGLEN(2) PAYLOAD(N) CRC(4) 
   
Where N >= 256 <=> `MTU` >= 40 + 8 + 1 + 4 + 2 + 1 + 2 + 2 + 2 + 1 + 2 + N + 4; that is 324 <= `MTU` <= 65535

Note that the very minimum `MTU` value for Carambolas.Net is actually higher (345 bytes) because secure packets have a bit more overhead. 

***Carambolas.Net requires the negotiated `MTU` to be valid or the connection is refused.***

*What's an ideal maximum `SEGLEN`?*

A few points must be taken into account:

  1) By increasing the maximum `SEGLEN` beyond 65535 we will be required to increase the footprint of both `SEGLEN` and `FRAGINDEX` in the fragment message;
  2) The bigger maximum `SEGLEN` becomes, the more memory a receiver will need to reserve to reassemble a complete segment;
  3) The protocol is intended for low-latency links with a small bandwidth-delay product, i.e. a rapid exchange of small packets;

It's been demonstrated that a maximum `SEGLEN` of 65535 can be achieved with a minimum overhead (only 3 bytes) over a normal single message segment as long as 
a minimum `MTU` value is enforced. Increasing the maximum `SEGLEN` beyond 65535 would only put pressure in the receiver given that most of the traffic is 
expected to be of small payloads (under 64KB in size). A maximum `SEGLEN` of 65535 does not preclude a user application from transfering large chunks of data
(i.e. large files) of more than 64KB, but then a custom fragmentation and reassembling strategy will have to be implemented. In such cases, however the user 
application is often in a position to do a better job than a generic library. For instance, an application that expects to transfer files of several megabytes 
may opt to buffer fragments directly on disk and save memory since the final goal is to produce a local file anyway.


### QoS

#### Reliable

#### Semireliable

#### Unreliable

#### Volatile

### Retransmissions

#### Timeout Retransmissions

#### Fast Retransmissions

#### Exponential Backoff


The acknowledgement timeout `ATO` mimics TCP's *RTO* as described by [RFC6298](https://tools.ietf.org/html/rfc6298) thus we support a binary exponential backoff 
the same way for retransmissions as proposed by Jacobson.

A side effect of this type of backoff, though, is that depending on the values of `SRTT` and `RTTVar`, the connection timeout `CTO` becomes dominant and 
hardly restricts the number of retransmission attempts. In a way, `CTO` imposes a soft upper limit to (`SRTT`, `RTTvar`) beyond which a lost packet will always 
trigger a disconnection (`CTO` <= `SRTT` + 4 * `RTTvar`)

The combination of connection timeout and ack timeout with backoff and ack fail limit may sometimes even result in an unexpected behaviour. This is because with a 
multiplicative backoff factor the time interval between consecutive ack timeouts (and eventual retransmissions) grows exponentially while connection timeout and 
ack fail limit are constants. The consequence is that depending on where the initial ack timeout (derived from the `RTT`) stands relative to a threshold the number 
of retransmissions will be limited by the ack fail limit and the total timeout to disconnect is going to be less than the connection timeout. As the initial ack 
timeout moves beyond this threshold, the reponse timeout becomes the limiting factor so the actual number of retransmissions amount to less than the ack fail limit.

The threshold in case can be calculated taking into account the backoff factor, the connection timeout and the ack fail limit.

The ack timeout (`ATO`) of the *i-th* transmission (i >= 0) is given by: *`ATO`<sub>i</sub> = `ATO`<sub>0</sub> * k<sup>i</sup>*, where *k* >= 1 is the ack 
backoff factor and *`ATO`<sub>0</sub>* is the initial `ATO` derived from the `RTT`. The equivalent recursive formulation is: 
*`ATO`<sub>i</sub> = `ATO`<sub>i-1</sub> * k, i > 0, k >= 1*

The partial sum for *n* transmissions is then given by: *`ATO`<sub>0</sub> * { 1 + k * [ ( k<sup>n-1</sup> ) -1 ] / ( k - 1 ) }, n > 0*

Note that the protocol defaults will produce a pretty aggresive retransmission behaviour with each retransmission taking up only 25% more time than the previous 
attempt.

The closer *k* gets to 0, the more aggressive retransmissions will be - i.e. closer in time.

Assuming a peer that never replies, the dynamic behaviour produced by the protocol defaults should be aproximately as follows:

Connection timeout (`CTO`) = 30s
Ack Backoff Factor (K) = 1.25s
Ack Fail Limit (`AFL`) = 10

| Initial Ack Timeout(s) | Number of transmissions (counting the first) | Total time(s) until disconnect |
|------------:|------------------------:|--------------:|
|        0.2  |          10             |       6.651   |
|        0.5  |          10             |      16.626   |                      
|        1.0  |          10             |      30.000   | 
|        2.0  |           7             |      30.000   |
|        4.0  |           5             |      30.000   |
|        8.0  |           3             |      30.000   |
|       16.0  |           2             |      30.000   |



Note that for the given parameters the limiting factor is `AFL` when `ATO`<sub>0</sub> < 1s and the disconnection is going to happen before `CTO`. After 
`ATO`<sub>0</sub> >= 1s, the limitation becomes the `CTO` with the number of transmissions that we can fit inside that time window decreasing. Once 
`ATO`<sub>0</sub> >= `CTO`/2, only one retransmission is ever possible so the connection becomes extremely sensitive to packet loss.

> ![Illustration of the retransmission curve for an initial timeout of 0.2s](Retransmission-Curve.jpg)
>
>*Retransmission curve for an initial timeout of 0.2s*

In general, `CTO` should be at least one order of manitude greater than the average `SRTT` projected for the connection. 
Assuming an `SRTT` = 400ms and `RTTVar` = 40ms as the norm (see [Reasonable real-life conditions](#reasonable-real-life-conditions))
then `CTO` could be as low as 5s. In fact given an `ATO`<sub>0</sub> it's possible to determine the minimum `CTO` needed for at least *n* retransmissions by:
       
`CTO` >= ∑<sup>n</sup><sub>i=0</sub> (2<sup>i</sup> * `ATO`<sub>0</sub>), ie. `CTO` >= 35s, *n* = 2 and `AFL` = 1 + *n*

The same relationship can be used to demonstrate that there's not much use in having `AFL` > 8 unless one can increase `CTO` exponentially to accomodate 
the extra retransmissions. 

In the best network conditions `ATO`<sub>0</sub> = `ATO`<sub>min</sub> = 200ms, the minimum `CTO` for at  least *n* retransmissions is given by:

`CTO` >= ∑<sup>n</sup><sub>i=0</sub> (2<sup>i</sup> * `ATO`<sub>0</sub>), ie. `CTO` >= 51s, *n* = 7 and `CTO` >= 102.2s, *n* = 8


### Flow control

#### Remote Window (aka Receive Window)

#### Congestion Window

#### Bandwidth Window



### Channels

### Encryption



## Implementation details


### IPEndPoint and IPAddress

Carambolas.Net works with its own IPAddress and IPEndPoint types implemented as efficient immutable value types. This is all due to System.Net.IPEndPoint 
producing a ridiculous amount of [unecessary allocations](https://github.com/dotnet/runtime/issues/30196). There is currently 
[a proposal](https://github.com/dotnet/runtime/issues/30797) (as of 2020-08-31) for a zero-allocation socket API in the dotnet/runtime but it's not finding
much support among developers because it would apparently impact the framework's design and ability to support protocols other than IPv4/IPv6.

The main goal of the immutable types is to have zero-allocations in order to send and receive data and this can be achieved when using the native socket 
library. Nevertheless, when using the fallback socket that relies on [System.Net.Sockets.Socket](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket), 
both Socket.ReceiveFrom and Socket.SendTo must ultimately depend on System.Net.IPEndPoint and there's no way but to accept about 6 or 7 allocations per call 
which may impose a little GC pressure. It's not expected to impact an individual connection's throughput (at least not until you start to reach for 800Mbps
and such) but may affect the overal performance of an application due to short interruptions caused by the GC. 


### Socket

Effectively a specialized non-blocking UDP socket that supports IPv4 and IPv6 (in single or dual mode) and may rely on a native library if avaliable for improved
performance. It bears many limitations and is not intended to be a general purpose socket implementation nor a full replacement for 
[System.Net.Sockets.Socket](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket). On the contrary, its goal is to provide only a minimum set of operations 
required by both [Host](#Host) and [Peers](#peer). 

Known native library issues:

* **Windows**:
  - One must we use Policy-based Quality of Service(QoS) to set DSCP/TOS in IP packets. Usually the configuration of a Policy-based Quality of Service is done 
    by defining a Group Policy Object(GPO) in the Group Policy Management Console(GPMC). This could be done for a single computer or distributed to a number of 
    computers via the domain controller. Note that the configuration option at "Policy-based QoS->Advanced QoS Settings->DSCP Marking Override" DOES NOT work 
    as expected. See https://social.technet.microsoft.com/Forums/en-US/eb440e1c-1fb0-4fa0-9801-3b9ae128f9ad/dscp-marking-override?forum=win10itpronetworking
* **Linux**:
  - Setting the IP DF flag directly (Don't Fragment) is not supported by the operating system. SocketOptionName.DontFragment is implemented by configuring the 
    socket for PMTUD;
* **Mac**:
  - SocketOptionName.DontFragment option is not supported by the operating system and is ignored;
  - SocketOptionName.TimeToLive option is not supported by the operating system and is ignored;
  
### Memory management


## Vulnerabilities


### Common to all session types

These vulnerabilities are common to both insecure and secure connections with some even common to other custom protocols that are also based on UDP/IP.

##### UDP Flood Attack

An attacker may send a large number of UDP datagrams either valid or invalid in order to overwhelm a host's ability to process and discard such datagrams.
The hosts's receive buffer may ultimately fill up completely. Most or all legitimate datagrams will be automatically discarded by the operating system eventually 
forcing the target to disconnect.

##### Starvation Attack

An attacker may initiate a normal connection and, taking advantage of the fact that a host's receive buffer is shared between all connected peers, start to 
aggressively transmit large messages disregarding all flow control indicators. If the attacker's upstream bandwidth is large enough, it may succeed in filling 
up the target's socket receive buffer, blocking packets from all other peers and force them to disconnect.

##### Malicious Packet Corruption

An attacker may intercept and randomly change bits in multiple packets before forwarding them again. By invalidating enough packets in succession an attacker 
may ultimately force a target to disconnect.


### Insecure Sessions


##### Reset Attack

An attacker possessing knowledge about the protocol and the session number used by a target (e.g. obtained by spoofing the network) may craft a `RST` packet that 
will cause the target to disconnect.

##### Fake Half-Open Connection Attack

An attacker possessing knowledge about the protocol and the latest `STM` transmitted by a host `A` may craft a `CON` packet and send it to `B` making it look 
like `A` is trying to recover from a half-open connection, causing `B` to reset (and eventually send an invalid `ACC` to `A`).

##### Malicious Packet Manipulation

An attacker possessing knowledge about the protocol may intercept a packet, modify message payloads, recalculate the `CRC` and forward the packet to its intended 
destination as a valid one. 

##### Packet Crafting Attack

An attacker possessing knowledge about the protocol and the next sequence number expected by the receiver in one or more channels may craft a complete new packet 
with malicious messages that once received will cause the target to drop further legitimate message(s) transmitted by the sender with those same sequence numbers 
while still producing a cumulative ACK that masquarades the situation from the sender.

### Secure Sessions

##### Private key stored in unprotected memory

A [Host](#host) must have access to a private key (at least while open) in order to be able to compute the shared key required for incoming secure connection
requests. .NET Standard 2.0 does not provide a portable way of specifying protected memory regions to store sensitive information such as secret keys and the 
alternative of repeatedly reading an encrypted key file from disk is inpractical. While .NET offers slightly better security against ordinary buffer overflow 
attacks, a private key will be in the clear if an attacker has access to the machine memory directly or indirectly (through memory dumps, for instance). In order 
to mitigate this problem, a host will by default operate with a random private key. This reduces the potential damage that may result from an attacker gaining access
to a host's memory but prevents remote key validation (since both ends will be exhanging randomly generated public keys) which leaves open the possiblity for a 
[man-in-the-middle attack during a secure connection handshake](#man-in-the-middle-attack) 

The host defaults to random private keys because there is no way for a user application to mitigate the risk of a private key residing in unprotected memory, whereas 
multiple alternatives exist to mitigate the risk of a [man-in-the-middle attack](#man-in-the-middle-attack).

##### Man-in-the-Middle Attack

During a secure connection handshake, an attacker may be able to impersonate a remote host by intercepting a connection request and sending back its own `SECACC` 
with a compromised public key. If the connecting host does not have previous knowledge about the legitimate remote host's public key (because both would be using 
randomly generated keys) there is no way for the connecting host to distinguish between a legitimate remote host and an attacker.

This vulnarability may be mitigated if the user application employs an identity validation scheme right after the connection is established. e.g. by using digital 
certificates.