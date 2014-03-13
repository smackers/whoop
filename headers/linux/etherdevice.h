#ifndef _LINUX_ETHERDEVICE_H
#define _LINUX_ETHERDEVICE_H

#include <linux/if_ether.h>
#include <linux/netdevice.h>

struct net_device *alloc_etherdev(int sizeof_priv)
{
	struct net_device *dev = (struct net_device *) malloc(sizeof_priv);
	return dev;
}

#endif	/* _LINUX_ETHERDEVICE_H */
