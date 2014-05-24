//xfail:DRIVER_ERROR
//

#include <linux/device.h>
#include <whoop.h>

struct shared {
	int resource;
};

static void entrypoint(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);

	tp->resource = 1;
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct test_device *dev = alloc_testdev(sizeof(*tp));

	tp = testdev_priv(dev);

	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint
};
