#ifndef _LINUX_NETDEVICE_H
#define _LINUX_NETDEVICE_H

#include <linux/kernel.h>
#include <linux/timer.h>
#include <linux/netdev_features.h>
#include <whoop.h>

#define	NETDEV_ALIGN 32

struct net_device {	
	netdev_features_t	features;
};

static inline void *netdev_priv(const struct net_device *dev)
{
	return (char *)dev + ALIGN(sizeof(struct net_device), NETDEV_ALIGN);
}

static inline bool netif_running(const struct net_device *dev)
{
	bool val;
	__SMACK_code("havoc @;", val);
	return val;
}

#endif	/* _LINUX_NETDEVICE_H */
