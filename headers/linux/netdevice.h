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

// Structure for NAPI scheduling
struct napi_struct {
	unsigned long state;
	int weight;
	struct net_device *dev;
	unsigned int napi_id;
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

void netif_device_attach(struct net_device *dev);
void netif_device_detach(struct net_device *dev);

void netif_stop_queue(struct net_device *dev);
void netif_tx_stop_all_queues(struct net_device *dev);

void napi_enable(struct napi_struct *n); // Enables NAPI from scheduling
void napi_disable(struct napi_struct *n); // Prevents NAPI from scheduling

#endif	/* _LINUX_NETDEVICE_H */
