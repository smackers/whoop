//xfail:DRIVER_ERROR
//

#include <linux/device.h>
#include <linux/netdevice.h>
#include <linux/etherdevice.h>
#include <asm/io.h>

struct shared {
	int resource;
	struct mutex mutex;
};

static void entrypoint(struct net_device *dev)
{
	struct shared *tp = netdev_priv(dev);
	
	tp->resource = 1;
	mutex_unlock(&tp->mutex);
}

void main()
{
	struct shared *tp;
	struct net_device *dev = alloc_etherdev(sizeof(*tp));
	
	tp = netdev_priv(dev);
	mutex_init(&tp->mutex);
	
	entrypoint(dev);
}
