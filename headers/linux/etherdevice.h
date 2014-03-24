#ifndef _LINUX_ETHERDEVICE_H
#define _LINUX_ETHERDEVICE_H

#include <linux/if_ether.h>
#include <linux/netdevice.h>

struct net_device *alloc_etherdev(int sizeof_priv)
{
	struct net_device *dev = (struct net_device *) malloc(sizeof_priv);
	return dev;
}

static inline bool is_valid_ether_addr(const u8 *addr)
{
	bool val;
	__SMACK_code("havoc @;", val);
	return val;
}

#endif	/* _LINUX_ETHERDEVICE_H */
