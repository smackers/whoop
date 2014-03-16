//pass
//

#include <linux/device.h>
#include <linux/netdevice.h>
#include <linux/etherdevice.h>
#include <asm/io.h>
#include <whoop.h>

struct shared {
	int resource;
	struct mutex mutex1;
	struct mutex mutex2;
};

static void entrypoint(struct net_device *dev)
{
	struct shared *tp = netdev_priv(dev);
	
	mutex_lock(&tp->mutex1);
	mutex_lock(&tp->mutex2);
	tp->resource = 1;
	mutex_unlock(&tp->mutex2);
	mutex_unlock(&tp->mutex1);
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct net_device *dev = alloc_etherdev(sizeof(*tp));
	
	tp = netdev_priv(dev);
	mutex_init(&tp->mutex1);
	mutex_init(&tp->mutex2);
	
	entrypoint(dev);
	
	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint
};
