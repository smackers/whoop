//xfail:DRIVER_ERROR
//

#include <linux/device.h>
#include <linux/netdevice.h>
#include <linux/etherdevice.h>
#include <asm/io.h>
#include <whoop.h>

struct shared {
	int resource;
	struct mutex mutex;
};

static void entrypoint(struct net_device *dev)
{
	struct shared *tp = netdev_priv(dev);
	
	mutex_lock(&tp->mutex);
	tp->resource = 1;
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct net_device *dev = alloc_etherdev(sizeof(*tp));
	
	tp = netdev_priv(dev);
	mutex_init(&tp->mutex);
	
	entrypoint(dev);
	
	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint
};
