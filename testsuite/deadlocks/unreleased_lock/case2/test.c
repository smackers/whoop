//xfail:DRIVER_ERROR
//

#include <linux/device.h>
#include <whoop.h>

struct shared {
	int resource;
	struct mutex mutex;
	struct mutex mutex2;
};

static void entrypoint1(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);

	mutex_lock(&tp->mutex);
	tp->resource = 1;
	mutex_unlock(&tp->mutex);
}

static void entrypoint2(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);

	mutex_lock(&tp->mutex2);
	tp->resource = 2;
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct test_device *dev = alloc_testdev(sizeof(*tp));

	tp = testdev_priv(dev);
	mutex_init(&tp->mutex);
	mutex_init(&tp->mutex2);

	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint1,
	.ep2 = entrypoint2
};