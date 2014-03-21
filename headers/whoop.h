//
// Whoop header files and definitions
//

#ifndef _WHOOP_H
#define _WHOOP_H

#include <smack.h>
#include <linux/kernel.h>

#define	TESTDEV_ALIGN 32
typedef u64 testdev_features_t;

struct test_device {
	testdev_features_t features;
};

struct test_device *alloc_testdev(int sizeof_priv)
{
	struct test_device *dev = (struct test_device *) malloc(sizeof_priv);
	return dev;
}

static inline void *testdev_priv(const struct test_device *dev)
{
	return (char *)dev + ALIGN(sizeof(struct test_device), TESTDEV_ALIGN);
}

struct test_driver {
	int (*probe) (struct device *dev);
	int  (*ep1) (struct test_device *dev);
	int  (*ep2) (struct test_device *dev);
	int  (*ep3) (struct test_device *dev);
	int  (*ep4) (struct test_device *dev);
	int  (*ep5) (struct test_device *dev);
};

#endif	/* _WHOOP_H */
