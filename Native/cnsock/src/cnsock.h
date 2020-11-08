#pragma once

#include <stdint.h>
#include "version.h"

#ifdef _WIN32
#ifndef WINDOWS
#define WINDOWS
#endif
#elif defined(__linux__)
#ifndef LINUX
#define LINUX
#endif
#elif defined(__APPLE__)
#ifndef OSX
#define OSX
#endif
#endif

#ifdef WINDOWS
    #define CARAMBOLAS_NET_EXPORT __declspec(dllexport)
#else
    #define CARAMBOLAS_NET_EXPORT extern
#endif

#ifdef WINDOWS
#include <ws2tcpip.h>
#else
#include <netinet/in.h>
#endif


#ifdef __cplusplus
extern "C" {
#endif

typedef int32_t carambolas_net_socket_t;
typedef int32_t carambolas_net_socket_error_t;

#define CARAMBOLAS_NET_SOCKET_AF_IPV4                                   2
#define CARAMBOLAS_NET_SOCKET_AF_IPV6                                  23

#define CARAMBOLAS_NET_SOCKET_ERROR                                    -1    // An unspecified error has occurred.
#define CARAMBOLAS_NET_SOCKET_ERROR_NONE                                0    // Operation succeeded.    
                                                                     
#define CARAMBOLAS_NET_SOCKET_ERROR_OPERATIONABORTED                  995    // The overlapped operation was aborted due to the closure of the System.Net.Sockets.Socket.
#define CARAMBOLAS_NET_SOCKET_ERROR_IOPENDING                         997    // The application has initiated an overlapped operation that cannot be completed immediately.
#define CARAMBOLAS_NET_SOCKET_ERROR_INTERRUPTED                     10004    // A blocking System.Net.Sockets.Socket call was canceled.
#define CARAMBOLAS_NET_SOCKET_ERROR_ACCESSDENIED                    10013    // An attempt was made to access a System.Net.Sockets.Socket in a way that is forbidden by its access permissions.
#define CARAMBOLAS_NET_SOCKET_ERROR_FAULT                           10014    // An invalid pointer address was detected by the underlying socket provider.
#define CARAMBOLAS_NET_SOCKET_ERROR_INVALIDARGUMENT                 10022    // An invalid argument was supplied to a System.Net.Sockets.Socket member.
#define CARAMBOLAS_NET_SOCKET_ERROR_TOOMANYOPENSOCKETS              10024    // There are too many open sockets in the underlying socket provider.
#define CARAMBOLAS_NET_SOCKET_ERROR_WOULDBLOCK                      10035    // An operation on a nonblocking socket cannot be completed immediately.
#define CARAMBOLAS_NET_SOCKET_ERROR_INPROGRESS                      10036    // A blocking operation is in progress.
#define CARAMBOLAS_NET_SOCKET_ERROR_ALREADYINPROGRESS               10037    // The nonblocking System.Net.Sockets.Socket already has an operation in progress.
#define CARAMBOLAS_NET_SOCKET_ERROR_NOTSOCKET                       10038    // A System.Net.Sockets.Socket operation was attempted on a non-socket.
#define CARAMBOLAS_NET_SOCKET_ERROR_DESTINATIONADDRESSREQUIRED      10039    // A required address was omitted from an operation on a System.Net.Sockets.Socket.
#define CARAMBOLAS_NET_SOCKET_ERROR_MESSAGESIZE                     10040    // The datagram is too long.
#define CARAMBOLAS_NET_SOCKET_ERROR_PROTOCOLTYPE                    10041    // The protocol type is incorrect for this System.Net.Sockets.Socket.
#define CARAMBOLAS_NET_SOCKET_ERROR_PROTOCOLOPTION                  10042    // An unknown, invalid, or unsupported option or level was used with a System.Net.Sockets.Socket.
#define CARAMBOLAS_NET_SOCKET_ERROR_PROTOCOLNOTSUPPORTED            10043    // The protocol is not implemented or has not been configured.
#define CARAMBOLAS_NET_SOCKET_ERROR_SOCKETNOTSUPPORTED              10044    // The support for the specified socket type does not exist in this address family.
#define CARAMBOLAS_NET_SOCKET_ERROR_OPERATIONNOTSUPPORTED           10045    // The address family is not supported by the protocol family.
#define CARAMBOLAS_NET_SOCKET_ERROR_PROTOCOLFAMILYNOTSUPPORTED      10046    // The protocol family is not implemented or has not been configured.
#define CARAMBOLAS_NET_SOCKET_ERROR_ADDRESSFAMILYNOTSUPPORTED       10047    // The address family specified is not supported. This error is returned if the IPv6 address family was specified and the IPv6 stack is not installed on the local machine. This error is returned if the IPv4 address family was specified and the IPv4 stack is not installed on the local machine.
#define CARAMBOLAS_NET_SOCKET_ERROR_ADDRESSALREADYINUSE             10048    // Only one use of an address is normally permitted.
#define CARAMBOLAS_NET_SOCKET_ERROR_ADDRESSNOTAVAILABLE             10049    // The selected IP address is not valid in this context.
#define CARAMBOLAS_NET_SOCKET_ERROR_NETWORKDOWN                     10050    // The network is not available.
#define CARAMBOLAS_NET_SOCKET_ERROR_NETWORKUNREACHABLE              10051    // No route to the remote host exists.
#define CARAMBOLAS_NET_SOCKET_ERROR_NETWORKRESET                    10052    // The application tried to set System.Net.Sockets.SocketOptionName.KeepAlive on a connection that has already timed out.
#define CARAMBOLAS_NET_SOCKET_ERROR_CONNECTIONABORTED               10053    // The connection was aborted by the .NET Framework or the underlying socket provider.
#define CARAMBOLAS_NET_SOCKET_ERROR_CONNECTIONRESET                 10054    // The connection was reset by the remote peer.
#define CARAMBOLAS_NET_SOCKET_ERROR_NOBUFFERSPACEAVAILABLE          10055    // No free buffer space is available for a System.Net.Sockets.Socket operation.
#define CARAMBOLAS_NET_SOCKET_ERROR_ISCONNECTED                     10056    // The System.Net.Sockets.Socket is already connected.
#define CARAMBOLAS_NET_SOCKET_ERROR_NOTCONNECTED                    10057    // The application tried to send or receive data, and the System.Net.Sockets.Socket is not connected.
#define CARAMBOLAS_NET_SOCKET_ERROR_SHUTDOWN                        10058    // A request to send or receive data was disallowed because the System.Net.Sockets.Socket has already been closed.
#define CARAMBOLAS_NET_SOCKET_ERROR_TIMEDOUT                        10060    // The connection attempt timed out, or the connected host has failed to respond.
#define CARAMBOLAS_NET_SOCKET_ERROR_CONNECTIONREFUSED               10061    // The remote host is actively refusing a connection.
#define CARAMBOLAS_NET_SOCKET_ERROR_HOSTDOWN                        10064    // The operation failed because the remote host is down.
#define CARAMBOLAS_NET_SOCKET_ERROR_HOSTUNREACHABLE                 10065    // There is no network route to the specified host.
#define CARAMBOLAS_NET_SOCKET_ERROR_PROCESSLIMIT                    10067    // Too many processes are using the underlying socket provider.
#define CARAMBOLAS_NET_SOCKET_ERROR_SYSTEMNOTREADY                  10091    // The network subsystem is unavailable.
#define CARAMBOLAS_NET_SOCKET_ERROR_VERSIONNOTSUPPORTED             10092    // The version of the underlying socket provider is out of range.
#define CARAMBOLAS_NET_SOCKET_ERROR_NOTINITIALIZED                  10093    // The underlying socket provider has not been initialized.
#define CARAMBOLAS_NET_SOCKET_ERROR_DISCONNECTING                   10101    // A graceful shutdown is in progress.
#define CARAMBOLAS_NET_SOCKET_ERROR_TYPENOTFOUND                    10109    // The specified class was not found.
#define CARAMBOLAS_NET_SOCKET_ERROR_HOSTNOTFOUND                    11001    // No such host is known. The name is not an official host name or alias.
#define CARAMBOLAS_NET_SOCKET_ERROR_TRYAGAIN                        11002    // The name of the host could not be resolved. Try again later.
#define CARAMBOLAS_NET_SOCKET_ERROR_NORECOVERY                      11003    // The error is unrecoverable or the requested database cannot be located.
#define CARAMBOLAS_NET_SOCKET_ERROR_NODATA                          11004    // The requested name or IP address was not found on the name server.


typedef struct 
{    
    union 
    {
        struct in6_addr ipv6;
        struct 
        {
            uint8_t zeros[10];
            uint16_t ffff;
            struct in_addr ipv4;
        };
    };    
    uint16_t family;
    uint16_t port;
} carambolas_net_socket_endpoint_t;

CARAMBOLAS_NET_EXPORT int32_t carambolas_net_initialize(void);

CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_open(int32_t addressFamily, carambolas_net_socket_t* sockfd);

CARAMBOLAS_NET_EXPORT void carambolas_net_socket_close(carambolas_net_socket_t sockfd);

CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_setsockopt(carambolas_net_socket_t sockfd, int32_t level, int32_t optname, int32_t optval);
CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_getsockopt(carambolas_net_socket_t sockfd, int32_t level, int32_t optname, int32_t* optval);
CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_setblocking(carambolas_net_socket_t  sockfd, int32_t value);

CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_bind(carambolas_net_socket_t sockfd, carambolas_net_socket_endpoint_t* endpoint);

CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_available(carambolas_net_socket_t sockfd, int32_t* nbytes);

CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_poll(carambolas_net_socket_t sockfd, int32_t microseconds, int32_t mode, int32_t* result);

CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_recvfrom(carambolas_net_socket_t sockfd, const uint8_t* buffer, int32_t offset, int32_t size, carambolas_net_socket_endpoint_t* endpoint, int32_t* nbytes);
CARAMBOLAS_NET_EXPORT carambolas_net_socket_error_t carambolas_net_socket_sendto(carambolas_net_socket_t sockfd, const uint8_t* buffer, int32_t offset, int32_t size, const carambolas_net_socket_endpoint_t* endpoint, int32_t* nbytes);

#ifdef __cplusplus
}
#endif