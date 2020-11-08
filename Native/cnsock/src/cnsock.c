#include "cnsock.h"

#include <stdlib.h>
#include <string.h>

#if defined(LINUX) || defined(OSX)
#include <netinet/tcp.h>
#include <arpa/inet.h>
#include <fcntl.h>
#include <netdb.h>
#include <unistd.h>
#include <sys/socket.h>
#include <sys/ioctl.h>
#include <errno.h>

#define SOCKET int32_t

#ifndef INVALID_SOCKET
#define INVALID_SOCKET                          (-1)
#endif

#ifndef closesocket
#define closesocket(fd)                         close(fd)
#endif
#endif

#ifdef SOCK_CLOEXEC

#define CARAMBOLAS_NET_SOCKET_TYPE              (SOCK_DGRAM | SOCK_CLOEXEC)

#else 

#define CARAMBOLAS_NET_SOCKET_TYPE              (SOCK_DGRAM)

#endif

#define CARAMBOLAS_NET_SOCKET_PROTO             (IPPROTO_UDP)

#ifdef WINDOWS

#define HAVE_IP_DONTFRAGMENT
#define SIO_UDP_CONNRESET                       (IOC_IN | IOC_VENDOR | 12)

#define carambolas_net_socket_getlasterror()    (WSAGetLastError())

#else

#ifdef LINUX
#define HAVE_IP_MTU_DISCOVER
#endif

enum
{
    CARAMBOLAS_NET_SOCKET_ERROR_SocketError = -1, // 0xFFFFFFFF
    CARAMBOLAS_NET_SOCKET_ERROR_Success = 0,
    CARAMBOLAS_NET_SOCKET_ERROR_OperationAborted = 995, // 0x000003E3
    CARAMBOLAS_NET_SOCKET_ERROR_IOPending = 997, // 0x000003E5
    CARAMBOLAS_NET_SOCKET_ERROR_Interrupted = 10004, // 0x00002714
    CARAMBOLAS_NET_SOCKET_ERROR_AccessDenied = 10013, // 0x0000271D
    CARAMBOLAS_NET_SOCKET_ERROR_Fault = 10014, // 0x0000271E
    CARAMBOLAS_NET_SOCKET_ERROR_InvalidArgument = 10022, // 0x00002726
    CARAMBOLAS_NET_SOCKET_ERROR_TooManyOpenSockets = 10024, // 0x00002728
    CARAMBOLAS_NET_SOCKET_ERROR_WouldBlock = 10035, // 0x00002733
    CARAMBOLAS_NET_SOCKET_ERROR_InProgress = 10036, // 0x00002734
    CARAMBOLAS_NET_SOCKET_ERROR_AlreadyInProgress = 10037, // 0x00002735
    CARAMBOLAS_NET_SOCKET_ERROR_NotSocket = 10038, // 0x00002736
    CARAMBOLAS_NET_SOCKET_ERROR_DestinationAddressRequired = 10039, // 0x00002737
    CARAMBOLAS_NET_SOCKET_ERROR_MessageSize = 10040, // 0x00002738
    CARAMBOLAS_NET_SOCKET_ERROR_ProtocolType = 10041, // 0x00002739
    CARAMBOLAS_NET_SOCKET_ERROR_ProtocolOption = 10042, // 0x0000273A
    CARAMBOLAS_NET_SOCKET_ERROR_ProtocolNotSupported = 10043, // 0x0000273B
    CARAMBOLAS_NET_SOCKET_ERROR_SocketNotSupported = 10044, // 0x0000273C
    CARAMBOLAS_NET_SOCKET_ERROR_OperationNotSupported = 10045, // 0x0000273D
    CARAMBOLAS_NET_SOCKET_ERROR_ProtocolFamilyNotSupported = 10046, // 0x0000273E
    CARAMBOLAS_NET_SOCKET_ERROR_AddressFamilyNotSupported = 10047, // 0x0000273F
    CARAMBOLAS_NET_SOCKET_ERROR_AddressAlreadyInUse = 10048, // 0x00002740
    CARAMBOLAS_NET_SOCKET_ERROR_AddressNotAvailable = 10049, // 0x00002741
    CARAMBOLAS_NET_SOCKET_ERROR_NetworkDown = 10050, // 0x00002742
    CARAMBOLAS_NET_SOCKET_ERROR_NetworkUnreachable = 10051, // 0x00002743
    CARAMBOLAS_NET_SOCKET_ERROR_NetworkReset = 10052, // 0x00002744
    CARAMBOLAS_NET_SOCKET_ERROR_ConnectionAborted = 10053, // 0x00002745
    CARAMBOLAS_NET_SOCKET_ERROR_ConnectionReset = 10054, // 0x00002746
    CARAMBOLAS_NET_SOCKET_ERROR_NoBufferSpaceAvailable = 10055, // 0x00002747
    CARAMBOLAS_NET_SOCKET_ERROR_IsConnected = 10056, // 0x00002748
    CARAMBOLAS_NET_SOCKET_ERROR_NotConnected = 10057, // 0x00002749
    CARAMBOLAS_NET_SOCKET_ERROR_Shutdown = 10058, // 0x0000274A
    CARAMBOLAS_NET_SOCKET_ERROR_TimedOut = 10060, // 0x0000274C
    CARAMBOLAS_NET_SOCKET_ERROR_ConnectionRefused = 10061, // 0x0000274D
    CARAMBOLAS_NET_SOCKET_ERROR_HostDown = 10064, // 0x00002750
    CARAMBOLAS_NET_SOCKET_ERROR_HostUnreachable = 10065, // 0x00002751
    CARAMBOLAS_NET_SOCKET_ERROR_ProcessLimit = 10067, // 0x00002753
    CARAMBOLAS_NET_SOCKET_ERROR_SystemNotReady = 10091, // 0x0000276B
    CARAMBOLAS_NET_SOCKET_ERROR_VersionNotSupported = 10092, // 0x0000276C
    CARAMBOLAS_NET_SOCKET_ERROR_NotInitialized = 10093, // 0x0000276D
    CARAMBOLAS_NET_SOCKET_ERROR_Disconnecting = 10101, // 0x00002775
    CARAMBOLAS_NET_SOCKET_ERROR_TypeNotFound = 10109, // 0x0000277D
    CARAMBOLAS_NET_SOCKET_ERROR_HostNotFound = 11001, // 0x00002AF9
    CARAMBOLAS_NET_SOCKET_ERROR_TryAgain = 11002, // 0x00002AFA
    CARAMBOLAS_NET_SOCKET_ERROR_NoRecovery = 11003, // 0x00002AFB
    CARAMBOLAS_NET_SOCKET_ERROR_NoData = 11004 // 0x00002AFC
};

static
carambolas_net_socket_error_t
carambolas_net_socket_geterror(int32_t code)
{    
    switch (code)
    {
        case EACCES: return CARAMBOLAS_NET_SOCKET_ERROR_AccessDenied;
        case EADDRINUSE: return CARAMBOLAS_NET_SOCKET_ERROR_AddressAlreadyInUse;
        case EADDRNOTAVAIL: return CARAMBOLAS_NET_SOCKET_ERROR_AddressNotAvailable;
        case EAFNOSUPPORT: return CARAMBOLAS_NET_SOCKET_ERROR_AddressFamilyNotSupported;
        case EAGAIN: return CARAMBOLAS_NET_SOCKET_ERROR_WouldBlock;
        case EALREADY: return CARAMBOLAS_NET_SOCKET_ERROR_AlreadyInProgress;
        case EBADF: return CARAMBOLAS_NET_SOCKET_ERROR_OperationAborted;
        case ECANCELED: return CARAMBOLAS_NET_SOCKET_ERROR_OperationAborted;
        case ECONNABORTED: return CARAMBOLAS_NET_SOCKET_ERROR_ConnectionAborted;
        case ECONNREFUSED: return CARAMBOLAS_NET_SOCKET_ERROR_ConnectionRefused;
        case ECONNRESET: return CARAMBOLAS_NET_SOCKET_ERROR_ConnectionReset;
        case EDESTADDRREQ: return CARAMBOLAS_NET_SOCKET_ERROR_DestinationAddressRequired;
        case EFAULT: return CARAMBOLAS_NET_SOCKET_ERROR_Fault;
        case EHOSTDOWN: return CARAMBOLAS_NET_SOCKET_ERROR_HostDown;
        case ENXIO: return CARAMBOLAS_NET_SOCKET_ERROR_HostNotFound; // not perfect: return but closest match available
        case EHOSTUNREACH: return CARAMBOLAS_NET_SOCKET_ERROR_HostUnreachable;
        case EINPROGRESS: return CARAMBOLAS_NET_SOCKET_ERROR_InProgress;
        case EINTR: return CARAMBOLAS_NET_SOCKET_ERROR_Interrupted;
        case EINVAL: return CARAMBOLAS_NET_SOCKET_ERROR_InvalidArgument;
        case EISCONN: return CARAMBOLAS_NET_SOCKET_ERROR_IsConnected;
        case EMFILE: return CARAMBOLAS_NET_SOCKET_ERROR_TooManyOpenSockets;
        case EMSGSIZE: return CARAMBOLAS_NET_SOCKET_ERROR_MessageSize;
        case ENETDOWN: return CARAMBOLAS_NET_SOCKET_ERROR_NetworkDown;
        case ENETRESET: return CARAMBOLAS_NET_SOCKET_ERROR_NetworkReset;
        case ENETUNREACH: return CARAMBOLAS_NET_SOCKET_ERROR_NetworkUnreachable;
        case ENFILE: return CARAMBOLAS_NET_SOCKET_ERROR_TooManyOpenSockets;
        case ENOBUFS: return CARAMBOLAS_NET_SOCKET_ERROR_NoBufferSpaceAvailable;
        case ENODATA: return CARAMBOLAS_NET_SOCKET_ERROR_NoData;
        case ENOENT: return CARAMBOLAS_NET_SOCKET_ERROR_AddressNotAvailable;
        case ENOPROTOOPT: return CARAMBOLAS_NET_SOCKET_ERROR_ProtocolOption;
        case ENOTCONN: return CARAMBOLAS_NET_SOCKET_ERROR_NotConnected;
        case ENOTSOCK: return CARAMBOLAS_NET_SOCKET_ERROR_NotSocket;
        case ENOTSUP: return CARAMBOLAS_NET_SOCKET_ERROR_OperationNotSupported;
        case EPERM: return CARAMBOLAS_NET_SOCKET_ERROR_AccessDenied;
        case EPIPE: return CARAMBOLAS_NET_SOCKET_ERROR_Shutdown;
        case EPFNOSUPPORT: return CARAMBOLAS_NET_SOCKET_ERROR_ProtocolFamilyNotSupported;
        case EPROTONOSUPPORT: return CARAMBOLAS_NET_SOCKET_ERROR_ProtocolNotSupported;
        case EPROTOTYPE: return CARAMBOLAS_NET_SOCKET_ERROR_ProtocolType;
        case ESOCKTNOSUPPORT: return CARAMBOLAS_NET_SOCKET_ERROR_SocketNotSupported;
        case ESHUTDOWN: return CARAMBOLAS_NET_SOCKET_ERROR_Disconnecting;
        case ETIMEDOUT: return CARAMBOLAS_NET_SOCKET_ERROR_TimedOut;  
        default: return CARAMBOLAS_NET_SOCKET_ERROR;
    }    
}

static
carambolas_net_socket_error_t
carambolas_net_socket_getlasterror()
{
    return carambolas_net_socket_geterror(errno);
}

enum SocketOptionLevel
{
    SocketOptionLevel_IP = 0,
    SocketOptionLevel_Tcp = 6,
    SocketOptionLevel_Udp = 17,
    SocketOptionLevel_IPv6 = 41,
    SocketOptionLevel_Socket = 65535
};

enum SocketOptionName
{
    SocketOptionName_DontLinger = -129,
    SocketOptionName_ExclusiveAddressUse = -5,
    SocketOptionName_Debug = 1,
    SocketOptionName_IPOptions = 1,
    SocketOptionName_NoChecksum = 1,
    SocketOptionName_NoDelay = 1,
    SocketOptionName_AcceptConnection = 2,
    SocketOptionName_BsdUrgent = 2,
    SocketOptionName_Expedited = 2,
    SocketOptionName_HeaderIncluded = 2,
    SocketOptionName_TypeOfService = 3,
    SocketOptionName_IpTimeToLive = 4,
    SocketOptionName_ReuseAddress = 4,
    SocketOptionName_KeepAlive = 8,
    SocketOptionName_MulticastInterface = 9,
    SocketOptionName_MulticastTimeToLive = 10,
    SocketOptionName_MulticastLoopback = 11,
    SocketOptionName_AddMembership = 12,
    SocketOptionName_DropMembership = 13,
    SocketOptionName_DontFragment = 14,
    SocketOptionName_AddSourceMembership = 15,
    SocketOptionName_DontRoute = 16,
    SocketOptionName_DropSourceMembership = 16,
    SocketOptionName_BlockSource = 17,
    SocketOptionName_UnblockSource = 18,
    SocketOptionName_PacketInformation = 19,
    SocketOptionName_ChecksumCoverage = 20,
    SocketOptionName_HopLimit = 21,
    SocketOptionName_IPProtectionLevel = 23,
    SocketOptionName_IPv6Only = 27,
    SocketOptionName_Broadcast = 32,
    SocketOptionName_UseLoopback = 64,
    SocketOptionName_Linger = 128,
    SocketOptionName_OutOfBandInline = 256,
    SocketOptionName_SendBuffer = 4097,
    SocketOptionName_ReceiveBuffer = 4098,
    SocketOptionName_SendLowWater = 4099,
    SocketOptionName_ReceiveLowWater = 4100,
    SocketOptionName_SendTimeout = 4101,
    SocketOptionName_ReceiveTimeout = 4102,
    SocketOptionName_Error = 4103,
    SocketOptionName_Type = 4104,
    SocketOptionName_ReuseUnicastPort = 12295,
    SocketOptionName_UpdateAcceptContext = 28683,
    SocketOptionName_UpdateConnectContext = 28688,
    SocketOptionName_MaxConnections = 0x7FFFFFFF
};

/*
 * Based on Mono Socket IO internal calls authored by
 *
 *  Dick Porter (dick@ximian.com)
 *  Gonzalo Paniagua Javier (gonzalo@ximian.com)
 *
 * Copyright 2001-2003 Ximian, Inc (http://www.ximian.com)
 * Copyright 2004-2009 Novell, Inc (http://www.novell.com)
 *
 * Published under the MIT License: http://opensource.org/licenses/MIT
 *
 * Returns:
 *    0 on success (mapped mono_level and mono_name to system_level and system_name
 *   -1 on error
 *   -2 on non-fatal error (ie, must ignore)
 */
static int32_t
carambolas_net_convert_sockopt(int32_t level, int32_t optname, int32_t *system_level, int32_t *system_name)
{
    switch (level)
    {
    case SocketOptionLevel_Socket:
        *system_level = SOL_SOCKET;

        switch (optname)
        {
        case SocketOptionName_DontLinger:
            /* This is SO_LINGER, because the setsockopt
             * internal call maps DontLinger to SO_LINGER
             * with l_onoff=0
             */
            *system_name = SO_LINGER;
            break;
#ifdef SO_DEBUG
        case SocketOptionName_Debug:
            *system_name = SO_DEBUG;
            break;
#endif
#ifdef SO_ACCEPTCONN
        case SocketOptionName_AcceptConnection:
            *system_name = SO_ACCEPTCONN;
            break;
#endif
        case SocketOptionName_ReuseAddress:
            *system_name = SO_REUSEADDR;
            break;
        case SocketOptionName_KeepAlive:
            *system_name = SO_KEEPALIVE;
            break;
#ifdef SO_DONTROUTE
        case SocketOptionName_DontRoute:
            *system_name = SO_DONTROUTE;
            break;
#endif
        case SocketOptionName_Broadcast:
            *system_name = SO_BROADCAST;
            break;
        case SocketOptionName_Linger:
            *system_name = SO_LINGER;
            break;
#ifdef SO_OOBINLINE
        case SocketOptionName_OutOfBandInline:
            *system_name = SO_OOBINLINE;
            break;
#endif
        case SocketOptionName_SendBuffer:
            *system_name = SO_SNDBUF;
            break;
        case SocketOptionName_ReceiveBuffer:
            *system_name = SO_RCVBUF;
            break;
        case SocketOptionName_SendLowWater:
            *system_name = SO_SNDLOWAT;
            break;
        case SocketOptionName_ReceiveLowWater:
            *system_name = SO_RCVLOWAT;
            break;
        case SocketOptionName_SendTimeout:
            *system_name = SO_SNDTIMEO;
            break;
        case SocketOptionName_ReceiveTimeout:
            *system_name = SO_RCVTIMEO;
            break;
        case SocketOptionName_Error:
            *system_name = SO_ERROR;
            break;
        case SocketOptionName_Type:
            *system_name = SO_TYPE;
            break;
        case SocketOptionName_ExclusiveAddressUse:
#ifdef SO_EXCLUSIVEADDRUSE
            *system_name = SO_EXCLUSIVEADDRUSE;
            break;
#endif
            return -2;
        case SocketOptionName_UseLoopback:
#ifdef SO_USELOOPBACK        
            *system_name = SO_USELOOPBACK;
            break;
#endif
            return -2;
        case SocketOptionName_MaxConnections:
#ifdef SO_MAXCONN
            *system_name = SO_MAXCONN;
            break;
#elif defined(SOMAXCONN)
            *system_name = SOMAXCONN;
            break;
#endif        
            return -2;
        default:
            return -1;
        }
        break;

    case SocketOptionLevel_IP:
        *system_level = IPPROTO_IP;

        switch (optname) {
#ifdef IP_OPTIONS
        case SocketOptionName_IPOptions:
            *system_name = IP_OPTIONS;
            break;
#endif
#ifdef IP_HDRINCL
        case SocketOptionName_HeaderIncluded:
            *system_name = IP_HDRINCL;
            break;
#endif
#ifdef IP_TOS
        case SocketOptionName_TypeOfService:
            *system_name = IP_TOS;
            break;
#endif
#ifdef IP_TTL
        case SocketOptionName_IpTimeToLive:
            *system_name = IP_TTL;
            break;
#endif
        case SocketOptionName_MulticastInterface:
            *system_name = IP_MULTICAST_IF;
            break;
        case SocketOptionName_MulticastTimeToLive:
            *system_name = IP_MULTICAST_TTL;
            break;
        case SocketOptionName_MulticastLoopback:
            *system_name = IP_MULTICAST_LOOP;
            break;
        case SocketOptionName_AddMembership:
            *system_name = IP_ADD_MEMBERSHIP;
            break;
        case SocketOptionName_DropMembership:
            *system_name = IP_DROP_MEMBERSHIP;
            break;
#ifdef HAVE_IP_PKTINFO
        case SocketOptionName_PacketInformation:
            *system_name = IP_PKTINFO;
            break;
#endif /* HAVE_IP_PKTINFO */

        case SocketOptionName_DontFragment:
#ifdef HAVE_IP_DONTFRAGMENT
            *system_name = IP_DONTFRAGMENT;
            break;
#elif defined HAVE_IP_MTU_DISCOVER
            /* Not quite the same */
            *system_name = IP_MTU_DISCOVER;
            break;
#else
            /* If the flag is not available on this system, we can ignore this error */
            return -2;
#endif /* HAVE_IP_DONTFRAGMENT */
        case SocketOptionName_AddSourceMembership:
        case SocketOptionName_DropSourceMembership:
        case SocketOptionName_BlockSource:
        case SocketOptionName_UnblockSource:
            /* Can't figure out how to map these, so fall
             * through
             */
        default:
            return -1;
        }
        break;

    case SocketOptionLevel_IPv6:
        *system_level = IPPROTO_IPV6;

        switch (optname)
        {
        case SocketOptionName_IpTimeToLive:
        case SocketOptionName_HopLimit:
            *system_name = IPV6_UNICAST_HOPS;
            break;
        case SocketOptionName_MulticastInterface:
            *system_name = IPV6_MULTICAST_IF;
            break;
        case SocketOptionName_MulticastTimeToLive:
            *system_name = IPV6_MULTICAST_HOPS;
            break;
        case SocketOptionName_MulticastLoopback:
            *system_name = IPV6_MULTICAST_LOOP;
            break;
        case SocketOptionName_AddMembership:
            *system_name = IPV6_JOIN_GROUP;
            break;
        case SocketOptionName_DropMembership:
            *system_name = IPV6_LEAVE_GROUP;
            break;
        case SocketOptionName_IPv6Only:
#ifdef IPV6_V6ONLY
            *system_name = IPV6_V6ONLY;
            break;
#else
            return -1;
#endif
        case SocketOptionName_PacketInformation:
#ifdef HAVE_IPV6_PKTINFO
            *system_name = IPV6_PKTINFO;
#endif
            break;
        case SocketOptionName_HeaderIncluded:
        case SocketOptionName_IPOptions:
        case SocketOptionName_TypeOfService:
        case SocketOptionName_DontFragment:
        case SocketOptionName_AddSourceMembership:
        case SocketOptionName_DropSourceMembership:
        case SocketOptionName_BlockSource:
        case SocketOptionName_UnblockSource:
            /* Can't figure out how to map these, so fall
             * through
             */
        default:
            return -1;
        }
        break;  /* SocketOptionLevel_IPv6 */

    case SocketOptionLevel_Tcp:
        *system_level = IPPROTO_TCP;

        switch (optname)
        {
        case SocketOptionName_NoDelay:
            *system_name = TCP_NODELAY;
            break;
#if 0
            /* The documentation is talking complete
             * bollocks here: rfc-1222 is titled
             * 'Advancing the NSFNET Routing Architecture'
             * and doesn't mention either of the words
             * "expedite" or "urgent".
             */
        case SocketOptionName_BsdUrgent:
        case SocketOptionName_Expedited:
#endif
        default:
            return -1;
        }
        break;

    case SocketOptionLevel_Udp:
        *system_level = IPPROTO_UDP;

        switch (optname)
        {
        case SocketOptionName_NoChecksum:
        case SocketOptionName_ChecksumCoverage:
        default:
            return -1;
        }
        return -1;

    default:
        return -1;
    }

    return 0;
}

#endif

int32_t
carambolas_net_initialize(void)
{
#ifdef WINDOWS    
    WSADATA wsaData = {0};
    if (WSAStartup(MAKEWORD(2, 2), &wsaData))
        return -1;

    if (LOBYTE(wsaData.wVersion) != 2 || HIBYTE(wsaData.wVersion) != 2) 
    {
        WSACleanup();
        return -1;
    }
#endif

    return 0;
}

carambolas_net_socket_error_t
carambolas_net_socket_open(int32_t addressFamily, carambolas_net_socket_t* sockfd)
{
    SOCKET handle;
    switch (addressFamily)
    {
    case CARAMBOLAS_NET_SOCKET_AF_IPV4: 
        handle = socket(PF_INET, CARAMBOLAS_NET_SOCKET_TYPE, CARAMBOLAS_NET_SOCKET_PROTO);
        break;
    case CARAMBOLAS_NET_SOCKET_AF_IPV6:
        handle = socket(PF_INET6, CARAMBOLAS_NET_SOCKET_TYPE, CARAMBOLAS_NET_SOCKET_PROTO);
        break;
    default:
        return CARAMBOLAS_NET_SOCKET_ERROR_ADDRESSFAMILYNOTSUPPORTED;
    }

    if (handle == INVALID_SOCKET)
        return carambolas_net_socket_getlasterror();

#ifdef WINDOWS    
    if (handle > 0xFFFFFFFF)
    {
        closesocket(handle);
        return CARAMBOLAS_NET_SOCKET_ERROR;
    }
#endif

    *sockfd = (carambolas_net_socket_t)handle;
    return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
}

void 
carambolas_net_socket_close(carambolas_net_socket_t sockfd)
{
    if (sockfd != INVALID_SOCKET) 
        closesocket(sockfd);
}

carambolas_net_socket_error_t 
carambolas_net_socket_setsockopt(carambolas_net_socket_t sockfd, int32_t level, int32_t optname, int32_t optval)
{
#ifdef WINDOWS
    if (setsockopt(sockfd, level, optname, (const char*)&optval, sizeof(optval)) == 0)
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#else
    int32_t r = carambolas_net_convert_sockopt(level, optname, &level, &optname);
    switch (r)
    {
    case 0: break;
    case -2: return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    default: return CARAMBOLAS_NET_SOCKET_ERROR_OperationNotSupported;
    }

    struct timeval tv;
    int32_t bufsize;

    const void* optptr = &optval;
    socklen_t optlen = sizeof(optval);

    if (level == SOL_SOCKET && (optname == SO_RCVTIMEO || optname == SO_SNDTIMEO))
    {
        tv.tv_sec = optval / 1000;
        tv.tv_usec = (optval % 1000) * 1000;    // micro from milli
        optptr = &tv;
        optlen = sizeof(tv);
    }
#if defined (LINUX)
    else if (level == SOL_SOCKET && (optname == SO_SNDBUF || optname == SO_RCVBUF))
    {
        // According to socket(7) the Linux kernel doubles the
        // buffer sizes "to allow space for bookkeeping
        // overhead."
        bufsize = optval;
        bufsize /= 2;
        optptr = &bufsize;
    }
#endif

    if (setsockopt(sockfd, level, optname, optptr, optlen) == 0)
    {
#if defined (SO_REUSEPORT)
        /* BSD's and MacOS X multicast sockets also need SO_REUSEPORT when SO_REUSEADDR is requested.  */
        if (level == SOL_SOCKET && optname == SO_REUSEADDR)
        {
            int32_t type;
            socklen_t type_len = sizeof(type);
            if (getsockopt(sockfd, level, SO_TYPE, &type, &type_len) == 0 && ((type != SOCK_DGRAM && type != SOCK_STREAM) || setsockopt(sockfd, level, SO_REUSEPORT, optptr, optlen) == 0))
                return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
        }
        else
        {
            return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
        }
#else 
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#endif
    }        
#endif

    return carambolas_net_socket_getlasterror();
}

carambolas_net_socket_error_t 
carambolas_net_socket_getsockopt(carambolas_net_socket_t sockfd, int32_t level, int32_t optname, int32_t* optval)
{
#ifdef WINDOWS
    socklen_t optlen = sizeof(int32_t);
    int32_t value;
    if (getsockopt(sockfd, level, optname, (char*)&value, &optlen) == 0)
    {
        *optval = value;
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }
#else 
    int32_t r = carambolas_net_convert_sockopt(level, optname, &level, &optname);
    switch (r)
    {
    case 0: break;
    case -2: return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    default: return CARAMBOLAS_NET_SOCKET_ERROR_OperationNotSupported;
    }

    struct timeval tv;
    socklen_t optlen = sizeof(int32_t);

    int32_t value;
    void* optptr = &value;

    if (level == SOL_SOCKET && (optname == SO_RCVTIMEO || optname == SO_SNDTIMEO)) 
    {
        optptr = &tv;
        optlen = sizeof(tv);
    }

    if (getsockopt(sockfd, level, optname, optptr, &optlen) == 0)
    {
        if (level == SOL_SOCKET && (optname == SO_RCVTIMEO || optname == SO_SNDTIMEO))
        {
            *optval = tv.tv_sec * 1000 + (tv.tv_usec / 1000);    // milli from micro
        }
        else if (optname == SO_ERROR)
            *optval = carambolas_net_socket_geterror(value);
        else
            *optval = value;

        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }

#endif    

    return carambolas_net_socket_getlasterror();
}

carambolas_net_socket_error_t 
carambolas_net_socket_setblocking(carambolas_net_socket_t sockfd, int32_t value)
{
#ifdef WINDOWS
    DWORD nonBlocking = ((value + 1) & 0x01);
    if (ioctlsocket(sockfd, FIONBIO, &nonBlocking) == 0)
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#else
    int nonBlocking = ((value + 1) & 0x01);
    if (fcntl(sockfd, F_SETFL, O_NONBLOCK, nonBlocking) == 0)
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#endif

    return carambolas_net_socket_getlasterror();
}

static
struct sockaddr_in
carambolas_net_socket_sockaddr_in(const carambolas_net_socket_endpoint_t* endpoint)
{
    struct sockaddr_in sa = {0};
    sa.sin_family = AF_INET;
    sa.sin_addr = endpoint->ipv4;
    sa.sin_port = htons(endpoint->port);
    return sa;
}

static
struct sockaddr_in6
carambolas_net_socket_sockaddr_in6(const carambolas_net_socket_endpoint_t* endpoint)
{
    struct sockaddr_in6 sa = {0};
    sa.sin6_family = AF_INET6;
    sa.sin6_addr = endpoint->ipv6;
    sa.sin6_port = htons(endpoint->port);
    return sa;
}


static
carambolas_net_socket_endpoint_t
carambolas_net_socket_endpoint(struct sockaddr_storage* source)
{
    carambolas_net_socket_endpoint_t endpoint = {0};

    if (source->ss_family == AF_INET)
    {
        struct sockaddr_in* sa = (struct sockaddr_in*)source;

        endpoint.ipv4 = sa->sin_addr;
        endpoint.family = CARAMBOLAS_NET_SOCKET_AF_IPV4;
        endpoint.port = ntohs(sa->sin_port);
    }
    else if (source->ss_family == AF_INET6)
    {
        struct sockaddr_in6* sa = (struct sockaddr_in6*)source;
        
        endpoint.ipv6 = sa->sin6_addr;
        endpoint.family = CARAMBOLAS_NET_SOCKET_AF_IPV6;
        endpoint.port = ntohs(sa->sin6_port);
    }

    return endpoint;
}

static 
carambolas_net_socket_error_t 
carambolas_net_socket_getsockname(carambolas_net_socket_t sockfd, carambolas_net_socket_endpoint_t* endpoint)
{
    struct sockaddr_storage sas = {0};
    socklen_t len = sizeof(sas);

    if (getsockname(sockfd, (struct sockaddr*)&sas, &len) == 0)
    {
        *endpoint = carambolas_net_socket_endpoint(&sas);
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }

    return carambolas_net_socket_getlasterror();
}


carambolas_net_socket_error_t 
carambolas_net_socket_bind(carambolas_net_socket_t sockfd, carambolas_net_socket_endpoint_t* endpoint)
{
    uint16_t af = endpoint->family;
    
    if (af == CARAMBOLAS_NET_SOCKET_AF_IPV4)
    {
        struct sockaddr_in sa = carambolas_net_socket_sockaddr_in(endpoint);

		if (bind(sockfd, (struct sockaddr*)&sa, sizeof(sa)) == 0 && carambolas_net_socket_getsockname(sockfd, endpoint) == 0)
		{
#ifdef WINDOWS
			int value = 0;
			if (ioctlsocket(sockfd, SIO_UDP_CONNRESET, &value) == 0)
				return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#else 
			return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#endif			
		}

        return carambolas_net_socket_getlasterror();
    }

    if (af == CARAMBOLAS_NET_SOCKET_AF_IPV6)
    {
        struct sockaddr_in6 sa = carambolas_net_socket_sockaddr_in6(endpoint);        

		if (bind(sockfd, (struct sockaddr*)&sa, sizeof(sa)) == 0 && carambolas_net_socket_getsockname(sockfd, endpoint) == 0)
		{
#ifdef WINDOWS
			int value = 0;
			if (ioctlsocket(sockfd, SIO_UDP_CONNRESET, &value) == 0)
				return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#else 
			return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
#endif			
		}

        return carambolas_net_socket_getlasterror();
    }

    return CARAMBOLAS_NET_SOCKET_ERROR_ADDRESSFAMILYNOTSUPPORTED;
}

carambolas_net_socket_error_t 
carambolas_net_socket_available(carambolas_net_socket_t sockfd, int32_t* nbytes)
{
#ifdef WINDOWS
    DWORD value;
    if (ioctlsocket(sockfd, FIONREAD, &value) == 0)
    {
        *nbytes = (int32_t)value;
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }
#else
    size_t value = 0;
    if (ioctl(sockfd, FIONREAD, &value) == 0)
    {
        *nbytes = (int32_t)value;
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }
#endif

    return carambolas_net_socket_getlasterror();
}

carambolas_net_socket_error_t 
carambolas_net_socket_poll(carambolas_net_socket_t sockfd, int32_t microseconds, int32_t mode, int32_t* result)
{
    fd_set readfds = {0};
    struct timeval time = {0};

    FD_ZERO(&readfds);
    FD_SET(sockfd, &readfds);

    time.tv_sec = microseconds / 1000000;
    time.tv_usec = microseconds % 1000000;

    int value = select((int)sockfd + 1, &readfds, NULL, NULL, &time);
    if (value < 0)
        return carambolas_net_socket_getlasterror();

    *result = value;
    return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
}

carambolas_net_socket_error_t 
carambolas_net_socket_recvfrom(carambolas_net_socket_t sockfd, const uint8_t* buffer, int32_t offset, int32_t size, carambolas_net_socket_endpoint_t* endpoint, int32_t* nbytes)
{
    struct sockaddr_storage sas = {0};
    socklen_t sas_len = sizeof(sas);

#ifdef WINDOWS
    *nbytes = recvfrom(sockfd, (char*)&buffer[offset], size, 0, (struct sockaddr*)&sas, &sas_len);
    if (*nbytes >= 0)
    {
        *endpoint = carambolas_net_socket_endpoint(&sas);
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }
#else
    *nbytes = recvfrom(sockfd, (char*)&buffer[offset], size, MSG_TRUNC, (struct sockaddr*)&sas, &sas_len);
    if (*nbytes > size)
    {
        *nbytes = size;
        *endpoint = carambolas_net_socket_endpoint(&sas);
        return CARAMBOLAS_NET_SOCKET_ERROR_MESSAGESIZE;
    }

    if (*nbytes >= 0)
    {
        *endpoint = carambolas_net_socket_endpoint(&sas);
        return CARAMBOLAS_NET_SOCKET_ERROR_NONE;
    }
#endif        

    return carambolas_net_socket_getlasterror();
}

carambolas_net_socket_error_t 
carambolas_net_socket_sendto(carambolas_net_socket_t sockfd, const uint8_t* buffer, int32_t offset, int32_t size, const carambolas_net_socket_endpoint_t* endpoint, int32_t* nbytes)
{
    uint16_t af = endpoint->family;

    if (af == CARAMBOLAS_NET_SOCKET_AF_IPV4)
    {
        struct sockaddr_in sa = carambolas_net_socket_sockaddr_in(endpoint);

        *nbytes = sendto(sockfd, (const char*)&buffer[offset], size, 0, (const struct sockaddr*)&sa, sizeof(sa));
        if (*nbytes >= 0)
            return CARAMBOLAS_NET_SOCKET_ERROR_NONE;

        return carambolas_net_socket_getlasterror();
    }

    if (af == CARAMBOLAS_NET_SOCKET_AF_IPV6)
    {
        struct sockaddr_in6 sa = carambolas_net_socket_sockaddr_in6(endpoint);

        *nbytes = sendto(sockfd, (const char*)&buffer[offset], size, 0, (const struct sockaddr*)&sa, sizeof(sa));
        if (*nbytes >= 0)
            return CARAMBOLAS_NET_SOCKET_ERROR_NONE;

        return carambolas_net_socket_getlasterror();
    }

    return CARAMBOLAS_NET_SOCKET_ERROR_ADDRESSFAMILYNOTSUPPORTED;
}