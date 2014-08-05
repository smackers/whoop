//pass
//

#include <linux/pci.h>
#include <linux/netdevice.h>
#include <linux/etherdevice.h>
#include <whoop.h>

struct shared {
	struct pci_dev *pci_dev;
	struct net_device *dev;
	int resource;
	struct mutex mutex;
};

static void entrypoint1(struct net_device *dev)
{
	struct shared *tp = netdev_priv(dev);

	mutex_lock(&tp->mutex);
	tp->resource = 1;
	mutex_unlock(&tp->mutex);
}

static void entrypoint2(struct device *device)
{
	struct pci_dev *pdev = to_pci_dev(device);
	struct net_device *dev = pci_get_drvdata(pdev);
	struct shared *tp = netdev_priv(dev);

	mutex_lock(&tp->mutex);
	tp->resource = 2;
	mutex_unlock(&tp->mutex);
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct net_device *dev;

	dev = alloc_etherdev(sizeof(*tp));
	SET_NETDEV_DEV(dev, &pdev->dev);

	tp = netdev_priv(dev);
	tp->dev = dev;
	tp->pci_dev = pdev;

	mutex_init(&tp->mutex);

out:
	return 0;
}

static const struct net_device_ops test_netdev_ops = {
	.ndo_open = entrypoint1,
	.ndo_stop = entrypoint2
};

static struct pci_driver test = {
	.probe = init
};
