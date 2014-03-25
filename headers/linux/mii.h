#ifndef __LINUX_MII_H__
#define __LINUX_MII_H__

struct mii_if_info {
	int phy_id;
	int advertising;
	int phy_id_mask;
	int reg_num_mask;

	unsigned int full_duplex : 1;	/* is full duplex? */
	unsigned int force_media : 1;	/* is autoneg. disabled? */
	unsigned int supports_gmii : 1; /* are GMII registers supported? */

	struct net_device *dev;
	int (*mdio_read) (struct net_device *dev, int phy_id, int location);
	void (*mdio_write) (struct net_device *dev, int phy_id, int location, int val);
};

struct mii_ioctl_data {
	__u16 phy_id;
	__u16 reg_num;
	__u16 val_in;
	__u16 val_out;
};

#endif /* __LINUX_MII_H__ */
