#ifndef _DEVICE_H_
#define _DEVICE_H_

#include <linux/ioport.h>
#include <linux/compiler.h>
#include <linux/mutex.h>
#include <linux/pm.h>
#include <linux/pm_wakeup.h>
#include <linux/pm_runtime.h>
#include <linux/list.h>

struct device_private {
	void *driver_data;
	struct device *device;
};

struct device {
	struct device *parent;
	struct device_private *p;
	void (*release)(struct device * dev);
};

struct device_driver {
	const char *name;

	int (*probe) (struct device *dev);
	int (*remove) (struct device *dev);
	void (*shutdown) (struct device *dev);
	int (*suspend) (struct device *dev, pm_message_t state);
	int (*resume) (struct device *dev);
	const struct attribute_group **groups;

	const struct dev_pm_ops *pm;
};

static inline void *dev_get_drvdata(struct device *dev);
static inline void dev_set_drvdata(struct device *dev, void *data);
static inline int dev_to_node(struct device *dev);

#define module_driver(__driver, __register, __unregister, ...) \
static int __init __driver##_init(void) \
{ \
	return __register(&(__driver) , ##__VA_ARGS__); \
} \
module_init(__driver##_init); \
static void __exit __driver##_exit(void) \
{ \
	__unregister(&(__driver) , ##__VA_ARGS__); \
} \
module_exit(__driver##_exit);

#endif /* _DEVICE_H_ */
