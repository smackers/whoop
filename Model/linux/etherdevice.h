#ifndef _LINUX_ETHERDEVICE_H
#define _LINUX_ETHERDEVICE_H

#include <linux/if_ether.h>
#include <linux/if_link.h>
#include <linux/netdevice.h>
#include <linux/crc32.h>

struct net_device *alloc_etherdev(int sizeof_priv)
{
	struct net_device *dev = (struct net_device *) malloc(sizeof_priv);

	// INIT_LIST_HEAD(&dev->napi_list);
//   INIT_LIST_HEAD(&dev->unreg_list);
//   INIT_LIST_HEAD(&dev->link_watch_list);
//   INIT_LIST_HEAD(&dev->upper_dev_list);
//   INIT_LIST_HEAD(&dev->lower_dev_list);

	return dev;
}

static inline bool is_valid_ether_addr(const u8 *addr)
{
	return true;
}

static inline int ether_crc(length, data)
{
	return 0;
}

int eth_validate_addr(struct net_device *dev);

#endif	/* _LINUX_ETHERDEVICE_H */
