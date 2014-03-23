//xfail:DRIVER_ERROR
//

#include <linux/device.h>
#include <whoop.h>

struct shared {
	int resource1;
	int resource2;
	struct mutex mutex;
};

static void entrypoint1(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);
	
	tp->resource1 = 1;
}

static void entrypoint2(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);
	
	tp->resource2 = 1;
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct test_device *dev = alloc_testdev(sizeof(*tp));
	
	tp = testdev_priv(dev);
	mutex_init(&tp->mutex);
	
	entrypoint1(dev);
	entrypoint2(dev);
	
	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint1,
	.ep2 = entrypoint2
};
