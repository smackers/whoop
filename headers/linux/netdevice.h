#ifndef _LINUX_NETDEVICE_H
#define _LINUX_NETDEVICE_H

#include <linux/kernel.h>
#include <linux/timer.h>
#include <linux/netdev_features.h>

#define	NETDEV_ALIGN 32

struct net_device {	
	netdev_features_t	features;
};

// Get network device private data.
static inline void *netdev_priv(const struct net_device *dev)
{
	// 
	// __ALIGN_KERNEL(x, a)            __ALIGN_KERNEL_MASK(x, (typeof(x))(a) - 1)
	// __ALIGN_KERNEL_MASK(x, mask)    (((x) + (mask)) & ~(mask))
	// 	
	// 	__ALIGN_KERNEL(sizeof(struct net_device), NETDEV_ALIGN)            __ALIGN_KERNEL_MASK(sizeof(struct net_device), (typeof(sizeof(struct net_device))) (NETDEV_ALIGN) - 1)
	// 	__ALIGN_KERNEL_MASK(x, mask)    (((x) + (mask)) & ~(mask))
	// 		
	// 		(((sizeof(struct net_device)) + ((typeof(sizeof(struct net_device))) (NETDEV_ALIGN) - 1)) & ~((typeof(sizeof(struct net_device))) (NETDEV_ALIGN) - 1))
	// 
	return (char *)dev + ALIGN(sizeof(struct net_device), NETDEV_ALIGN);
}

// struct net_device {
// 
// 	/*
// 	 * This is the first field of the "visible" part of this structure
// 	 * (i.e. as seen by users in the "Space.c" file).  It is the name
// 	 * of the interface.
// 	 */
// 	char			name[IFNAMSIZ];
// 
// 	/* device name hash chain, please keep it close to name[] */
// 	struct hlist_node	name_hlist;
// 
// 	/* snmp alias */
// 	char 			*ifalias;
// 
// 	/*
// 	 *	I/O specific fields
// 	 *	FIXME: Merge these and struct ifmap into one
// 	 */
// 	unsigned long		mem_end;	/* shared mem end	*/
// 	unsigned long		mem_start;	/* shared mem start	*/
// 	unsigned long		base_addr;	/* device I/O address	*/
// 	unsigned int		irq;		/* device IRQ number	*/
// 
// 	/*
// 	 *	Some hardware also needs these fields, but they are not
// 	 *	part of the usual set specified in Space.c.
// 	 */
// 
// 	unsigned long		state;
// 
// 	struct list_head	dev_list;
// 	struct list_head	napi_list;
// 	struct list_head	unreg_list;
// 	struct list_head	upper_dev_list; /* List of upper devices */
// 	struct list_head	lower_dev_list;
// 
// 
// 	/* currently active device features */
// 	netdev_features_t	features;
// 	/* user-changeable features */
// 	netdev_features_t	hw_features;
// 	/* user-requested features */
// 	netdev_features_t	wanted_features;
// 	/* mask of features inheritable by VLAN devices */
// 	netdev_features_t	vlan_features;
// 	/* mask of features inherited by encapsulating devices
// 	 * This field indicates what encapsulation offloads
// 	 * the hardware is capable of doing, and drivers will
// 	 * need to set them appropriately.
// 	 */
// 	netdev_features_t	hw_enc_features;
// 	/* mask of fetures inheritable by MPLS */
// 	netdev_features_t	mpls_features;
// 
// 	/* Interface index. Unique device identifier	*/
// 	int			ifindex;
// 	int			iflink;
// 
// 	struct net_device_stats	stats;
// 	atomic_long_t		rx_dropped; /* dropped packets by core network
// 					     * Do not use this in drivers.
// 					     */
// 
// #ifdef CONFIG_WIRELESS_EXT
// 	/* List of functions to handle Wireless Extensions (instead of ioctl).
// 	 * See <net/iw_handler.h> for details. Jean II */
// 	const struct iw_handler_def *	wireless_handlers;
// 	/* Instance data managed by the core of Wireless Extensions. */
// 	struct iw_public_data *	wireless_data;
// #endif
// 	/* Management operations */
// 	const struct net_device_ops *netdev_ops;
// 	const struct ethtool_ops *ethtool_ops;
// 
// 	/* Hardware header description */
// 	const struct header_ops *header_ops;
// 
// 	unsigned int		flags;	/* interface flags (a la BSD)	*/
// 	unsigned int		priv_flags; /* Like 'flags' but invisible to userspace.
// 					     * See if.h for definitions. */
// 	unsigned short		gflags;
// 	unsigned short		padded;	/* How much padding added by alloc_netdev() */
// 
// 	unsigned char		operstate; /* RFC2863 operstate */
// 	unsigned char		link_mode; /* mapping policy to operstate */
// 
// 	unsigned char		if_port;	/* Selectable AUI, TP,..*/
// 	unsigned char		dma;		/* DMA channel		*/
// 
// 	unsigned int		mtu;	/* interface MTU value		*/
// 	unsigned short		type;	/* interface hardware type	*/
// 	unsigned short		hard_header_len;	/* hardware hdr length	*/
// 
// 	/* extra head- and tailroom the hardware may need, but not in all cases
// 	 * can this be guaranteed, especially tailroom. Some cases also use
// 	 * LL_MAX_HEADER instead to allocate the skb.
// 	 */
// 	unsigned short		needed_headroom;
// 	unsigned short		needed_tailroom;
// 
// 	/* Interface address info. */
// 	unsigned char		perm_addr[MAX_ADDR_LEN]; /* permanent hw address */
// 	unsigned char		addr_assign_type; /* hw address assignment type */
// 	unsigned char		addr_len;	/* hardware address length	*/
// 	unsigned char		neigh_priv_len;
// 	unsigned short          dev_id;		/* Used to differentiate devices
// 						 * that share the same link
// 						 * layer address
// 						 */
// 	spinlock_t		addr_list_lock;
// 	struct netdev_hw_addr_list	uc;	/* Unicast mac addresses */
// 	struct netdev_hw_addr_list	mc;	/* Multicast mac addresses */
// 	struct netdev_hw_addr_list	dev_addrs; /* list of device
// 						    * hw addresses
// 						    */
// #ifdef CONFIG_SYSFS
// 	struct kset		*queues_kset;
// #endif
// 
// 	bool			uc_promisc;
// 	unsigned int		promiscuity;
// 	unsigned int		allmulti;
// 
// 
// 	/* Protocol specific pointers */
// 
// #if IS_ENABLED(CONFIG_VLAN_8021Q)
// 	struct vlan_info __rcu	*vlan_info;	/* VLAN info */
// #endif
// #if IS_ENABLED(CONFIG_NET_DSA)
// 	struct dsa_switch_tree	*dsa_ptr;	/* dsa specific data */
// #endif
// 	void 			*atalk_ptr;	/* AppleTalk link 	*/
// 	struct in_device __rcu	*ip_ptr;	/* IPv4 specific data	*/
// 	struct dn_dev __rcu     *dn_ptr;        /* DECnet specific data */
// 	struct inet6_dev __rcu	*ip6_ptr;       /* IPv6 specific data */
// 	void			*ax25_ptr;	/* AX.25 specific data */
// 	struct wireless_dev	*ieee80211_ptr;	/* IEEE 802.11 specific data,
// 						   assign before registering */
// 
// /*
//  * Cache lines mostly used on receive path (including eth_type_trans())
//  */
// 	unsigned long		last_rx;	/* Time of last Rx
// 						 * This should not be set in
// 						 * drivers, unless really needed,
// 						 * because network stack (bonding)
// 						 * use it if/when necessary, to
// 						 * avoid dirtying this cache line.
// 						 */
// 
// 	/* Interface address info used in eth_type_trans() */
// 	unsigned char		*dev_addr;	/* hw address, (before bcast
// 						   because most packets are
// 						   unicast) */
// 
// 
// #ifdef CONFIG_RPS
// 	struct netdev_rx_queue	*_rx;
// 
// 	/* Number of RX queues allocated at register_netdev() time */
// 	unsigned int		num_rx_queues;
// 
// 	/* Number of RX queues currently active in device */
// 	unsigned int		real_num_rx_queues;
// 
// #endif
// 
// 	rx_handler_func_t __rcu	*rx_handler;
// 	void __rcu		*rx_handler_data;
// 
// 	struct netdev_queue __rcu *ingress_queue;
// 	unsigned char		broadcast[MAX_ADDR_LEN];	/* hw bcast add	*/
// 
// 
// /*
//  * Cache lines mostly used on transmit path
//  */
// 	struct netdev_queue	*_tx ____cacheline_aligned_in_smp;
// 
// 	/* Number of TX queues allocated at alloc_netdev_mq() time  */
// 	unsigned int		num_tx_queues;
// 
// 	/* Number of TX queues currently active in device  */
// 	unsigned int		real_num_tx_queues;
// 
// 	/* root qdisc from userspace point of view */
// 	struct Qdisc		*qdisc;
// 
// 	unsigned long		tx_queue_len;	/* Max frames per queue allowed */
// 	spinlock_t		tx_global_lock;
// 
// #ifdef CONFIG_XPS
// 	struct xps_dev_maps __rcu *xps_maps;
// #endif
// #ifdef CONFIG_RFS_ACCEL
// 	/* CPU reverse-mapping for RX completion interrupts, indexed
// 	 * by RX queue number.  Assigned by driver.  This must only be
// 	 * set if the ndo_rx_flow_steer operation is defined. */
// 	struct cpu_rmap		*rx_cpu_rmap;
// #endif
// 
// 	/* These may be needed for future network-power-down code. */
// 
// 	/*
// 	 * trans_start here is expensive for high speed devices on SMP,
// 	 * please use netdev_queue->trans_start instead.
// 	 */
// 	unsigned long		trans_start;	/* Time (in jiffies) of last Tx	*/
// 
// 	int			watchdog_timeo; /* used by dev_watchdog() */
// 	struct timer_list	watchdog_timer;
// 
// 	/* Number of references to this device */
// 	int __percpu		*pcpu_refcnt;
// 
// 	/* delayed register/unregister */
// 	struct list_head	todo_list;
// 	/* device index hash chain */
// 	struct hlist_node	index_hlist;
// 
// 	struct list_head	link_watch_list;
// 
// 	/* register/unregister state machine */
// 	enum { NETREG_UNINITIALIZED=0,
// 	       NETREG_REGISTERED,	/* completed register_netdevice */
// 	       NETREG_UNREGISTERING,	/* called unregister_netdevice */
// 	       NETREG_UNREGISTERED,	/* completed unregister todo */
// 	       NETREG_RELEASED,		/* called free_netdev */
// 	       NETREG_DUMMY,		/* dummy device for NAPI poll */
// 	} reg_state:8;
// 
// 	bool dismantle; /* device is going do be freed */
// 
// 	enum {
// 		RTNL_LINK_INITIALIZED,
// 		RTNL_LINK_INITIALIZING,
// 	} rtnl_link_state:16;
// 
// 	/* Called from unregister, can be used to call free_netdev */
// 	void (*destructor)(struct net_device *dev);
// 
// #ifdef CONFIG_NETPOLL
// 	struct netpoll_info __rcu	*npinfo;
// #endif
// 
// #ifdef CONFIG_NET_NS
// 	/* Network namespace this network device is inside */
// 	struct net		*nd_net;
// #endif
// 
// 	/* mid-layer private */
// 	union {
// 		void				*ml_priv;
// 		struct pcpu_lstats __percpu	*lstats; /* loopback stats */
// 		struct pcpu_tstats __percpu	*tstats; /* tunnel stats */
// 		struct pcpu_dstats __percpu	*dstats; /* dummy stats */
// 		struct pcpu_vstats __percpu	*vstats; /* veth stats */
// 	};
// 	/* GARP */
// 	struct garp_port __rcu	*garp_port;
// 	/* MRP */
// 	struct mrp_port __rcu	*mrp_port;
// 
// 	/* class/net/name entry */
// 	struct device		dev;
// 	/* space for optional device, statistics, and wireless sysfs groups */
// 	const struct attribute_group *sysfs_groups[4];
// 
// 	/* rtnetlink link ops */
// 	const struct rtnl_link_ops *rtnl_link_ops;
// 
// 	/* for setting kernel sock attribute on TCP connection setup */
// #define GSO_MAX_SIZE		65536
// 	unsigned int		gso_max_size;
// #define GSO_MAX_SEGS		65535
// 	u16			gso_max_segs;
// 
// #ifdef CONFIG_DCB
// 	/* Data Center Bridging netlink ops */
// 	const struct dcbnl_rtnl_ops *dcbnl_ops;
// #endif
// 	u8 num_tc;
// 	struct netdev_tc_txq tc_to_txq[TC_MAX_QUEUE];
// 	u8 prio_tc_map[TC_BITMASK + 1];
// 
// #if IS_ENABLED(CONFIG_FCOE)
// 	/* max exchange id for FCoE LRO by ddp */
// 	unsigned int		fcoe_ddp_xid;
// #endif
// #if IS_ENABLED(CONFIG_NETPRIO_CGROUP)
// 	struct netprio_map __rcu *priomap;
// #endif
// 	/* phy device may attach itself for hardware timestamping */
// 	struct phy_device *phydev;
// 
// 	struct lock_class_key *qdisc_tx_busylock;
// 
// 	/* group the device belongs to */
// 	int group;
// 
// 	struct pm_qos_request	pm_qos_req;
// };

#endif	/* _LINUX_NETDEVICE_H */
