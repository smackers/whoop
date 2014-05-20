//pass
//

#include <linux/device.h>
#include <whoop.h>

struct shared {
	int resource;
	struct mutex mutex;
};

static void entrypoint1(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);
	
	mutex_lock(&tp->mutex);
	if (tp->resource = 2)
		tp->resource = 1;
	mutex_unlock(&tp->mutex);
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct test_device *dev = alloc_testdev(sizeof(*tp));
	
	tp = testdev_priv(dev);
	mutex_init(&tp->mutex);
	
	entrypoint1(dev);
	
	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint1
};
