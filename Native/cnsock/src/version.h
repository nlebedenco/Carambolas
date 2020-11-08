#pragma once

#define STRX(s)     #s
#define STR(s)      STRX(s)
 
#define CARAMBOLAS_NET_VERSION_MAJOR          0
#define CARAMBOLAS_NET_VERSION_MINOR          1
#define CARAMBOLAS_NET_VERSION_BUILD          0
#define CARAMBOLAS_NET_VERSION_REVISION       0
 
#define CARAMBOLAS_NET_FILE_VERSION           CARAMBOLAS_NET_VERSION_MAJOR, CARAMBOLAS_NET_VERSION_MINOR, CARAMBOLAS_NET_VERSION_BUILD, CARAMBOLAS_NET_VERSION_REVISION
#define CARAMBOLAS_NET_FILE_VERSION_STR       STR(CARAMBOLAS_NET_VERSION_MAJOR)         \
                                               "." STR(CARAMBOLAS_NET_VERSION_MINOR)    \
                                               "." STR(CARAMBOLAS_NET_VERSION_BUILD)    \
                                               "." STR(CARAMBOLAS_NET_VERSION_REVISION) \
 
#define CARAMBOLAS_NET_PRODUCT_VERSION        CARAMBOLAS_NET_VERSION_MAJOR, CARAMBOLAS_NET_VERSION_MINOR, 0, 0
#define CARAMBOLAS_NET_PRODUCT_VERSION_STR    STR(CARAMBOLAS_NET_VERSION_MAJOR)         \
                                               "." STR(CARAMBOLAS_NET_VERSION_MINOR)    \
