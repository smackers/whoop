#ifndef _LINUX_NETDEVICE_H
#define _LINUX_NETDEVICE_H

#include <linux/kernel.h>
#include <linux/timer.h>
#include <linux/netdev_features.h>
#include <linux/socket.h>
#include <whoop.h>

#define	NETDEV_ALIGN 32

enum netdev_tx {
	__NETDEV_TX_MIN	 = INT_MIN,
	NETDEV_TX_OK	 = 0x00,
	NETDEV_TX_BUSY	 = 0x10,
	NETDEV_TX_LOCKED = 0x20,
};
typedef enum netdev_tx netdev_tx_t;

struct net_device {
	netdev_features_t	features;	
	netdev_features_t	hw_features;
	netdev_features_t	wanted_features;
	netdev_features_t	vlan_features;
	netdev_features_t	hw_enc_features;
	netdev_features_t	mpls_features;
	
	unsigned char		addr_len;	// hardware address length
	unsigned char *dev_addr; // hw address
};

struct net_device_ops {
	int (*ndo_init)(struct net_device *dev);
	void (*ndo_uninit)(struct net_device *dev);
	int (*ndo_open)(struct net_device *dev);
	int (*ndo_stop)(struct net_device *dev);
	netdev_tx_t (*ndo_start_xmit) (struct sk_buff *skb, struct net_device *dev);
	u16 (*ndo_select_queue)(struct net_device *dev, struct sk_buff *skb);
	void (*ndo_change_rx_flags)(struct net_device *dev, int flags);
	void (*ndo_set_rx_mode)(struct net_device *dev);
	int (*ndo_set_mac_address)(struct net_device *dev, void *addr);
	int (*ndo_validate_addr)(struct net_device *dev);
	int (*ndo_do_ioctl)(struct net_device *dev, struct ifreq *ifr, int cmd);
	int (*ndo_set_config)(struct net_device *dev, struct ifmap *map);
	int (*ndo_change_mtu)(struct net_device *dev, int new_mtu);
	int (*ndo_neigh_setup)(struct net_device *dev, struct neigh_parms *);
	void (*ndo_tx_timeout) (struct net_device *dev);

	struct rtnl_link_stats64* (*ndo_get_stats64)(struct net_device *dev, struct rtnl_link_stats64 *storage);
	struct net_device_stats* (*ndo_get_stats)(struct net_device *dev);

	int (*ndo_vlan_rx_add_vid)(struct net_device *dev, __be16 proto, u16 vid);
	int (*ndo_vlan_rx_kill_vid)(struct net_device *dev, __be16 proto, u16 vid);
	void (*ndo_poll_controller)(struct net_device *dev);
	int (*ndo_netpoll_setup)(struct net_device *dev, struct netpoll_info *info, gfp_t gfp);
	void (*ndo_netpoll_cleanup)(struct net_device *dev);
	int (*ndo_busy_poll)(struct napi_struct *dev);
	int (*ndo_set_vf_mac)(struct net_device *dev, int queue, u8 *mac);
	int (*ndo_set_vf_vlan)(struct net_device *dev, int queue, u16 vlan, u8 qos);
	int (*ndo_set_vf_tx_rate)(struct net_device *dev, int vf, int rate);
	int (*ndo_set_vf_spoofchk)(struct net_device *dev, int vf, bool setting);
	int (*ndo_get_vf_config)(struct net_device *dev, int vf, struct ifla_vf_info *ivf);
	int (*ndo_set_vf_link_state)(struct net_device *dev, int vf, int link_state);
	int (*ndo_set_vf_port)(struct net_device *dev, int vf, struct nlattr *port[]);
	int (*ndo_get_vf_port)(struct net_device *dev, int vf, struct sk_buff *skb);
	int (*ndo_setup_tc)(struct net_device *dev, u8 tc);
	int (*ndo_fcoe_enable)(struct net_device *dev);
	int (*ndo_fcoe_disable)(struct net_device *dev);
	int (*ndo_fcoe_ddp_setup)(struct net_device *dev, u16 xid, struct scatterlist *sgl, unsigned int sgc);
	int (*ndo_fcoe_ddp_done)(struct net_device *dev, u16 xid);
	int (*ndo_fcoe_ddp_target)(struct net_device *dev, u16 xid, struct scatterlist *sgl, unsigned int sgc);
	int (*ndo_fcoe_get_hbainfo)(struct net_device *dev, struct netdev_fcoe_hbainfo *hbainfo);

#define NETDEV_FCOE_WWNN 0
#define NETDEV_FCOE_WWPN 1
	
	int (*ndo_fcoe_get_wwn)(struct net_device *dev, u64 *wwn, int type);
	int (*ndo_rx_flow_steer)(struct net_device *dev, const struct sk_buff *skb, u16 rxq_index, u32 flow_id);
	int (*ndo_add_slave)(struct net_device *dev, struct net_device *slave_dev);
	int (*ndo_del_slave)(struct net_device *dev, struct net_device *slave_dev);
	netdev_features_t	(*ndo_fix_features)(struct net_device *dev, netdev_features_t features);
	int (*ndo_set_features)(struct net_device *dev, netdev_features_t features);
	int (*ndo_neigh_construct)(struct neighbour *n);
	void (*ndo_neigh_destroy)(struct neighbour *n);
	int (*ndo_fdb_add)(struct ndmsg *ndm, struct nlattr *tb[], struct net_device *dev, const unsigned char *addr, u16 flags);
	int (*ndo_fdb_del)(struct ndmsg *ndm, struct nlattr *tb[], struct net_device *dev, const unsigned char *addr);
	int (*ndo_fdb_dump)(struct sk_buff *skb, struct netlink_callback *cb, struct net_device *dev, int idx);
	int (*ndo_bridge_setlink)(struct net_device *dev, struct nlmsghdr *nlh);
	int (*ndo_bridge_getlink)(struct sk_buff *skb, u32 pid, u32 seq, struct net_device *dev, u32 filter_mask);
	int (*ndo_bridge_dellink)(struct net_device *dev, struct nlmsghdr *nlh);
	int (*ndo_change_carrier)(struct net_device *dev, bool new_carrier);
	int (*ndo_get_phys_port_id)(struct net_device *dev, struct netdev_phys_port_id *ppid);
	// void (*ndo_add_vxlan_port)(struct  net_device *dev, sa_family_t sa_family, __be16 port);
	// void (*ndo_del_vxlan_port)(struct  net_device *dev, sa_family_t sa_family, __be16 port);
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
void napi_schedule(struct napi_struct *n); // Schedule NAPI poll routine to be called if it is not already running

#endif	/* _LINUX_NETDEVICE_H */
