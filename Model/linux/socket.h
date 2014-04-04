#ifndef _LINUX_SOCKET_H
#define _LINUX_SOCKET_H

#include <linux/sockios.h>

struct sockaddr {
	short	sa_family;
	char sa_data[14];
};

#endif /* _LINUX_SOCKET_H */
