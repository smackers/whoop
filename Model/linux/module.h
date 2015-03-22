#ifndef _LINUX_MODULE_H
#define _LINUX_MODULE_H

#include <linux/types.h>
#include <linux/moduleparam.h>
#include <linux/sched.h>
#include <linux/platform_device.h>
#include <linux/pci.h>
#include <linux/poll.h>

#include <asm/sn/tiocx.h>

#define MODULE_INFO(tag, info) void
#define MODULE_AUTHOR(_author) void
#define MODULE_DESCRIPTION(_description) void
#define MODULE_LICENSE(_license) void
#define MODULE_VERSION(_version) void
#define MODULE_FIRMWARE(_firmware) void
#define MODULE_ALIAS(_alias) void
#define MODULE_ALIAS_CHARDEV_MAJOR(_alias) void
#define MODULE_ALIAS_BLOCKDEV_MAJOR(_alias) void
#define MODULE_ALIAS_MISCDEV(_alias) void
#define MODULE_SOFTDEP(_softdep) void
#define MODULE_SUPPORTED_DEVICE(name) void
#define MODULE_GENERIC_TABLE(gtype,name) void
#define MODULE_DEVICE_TABLE(type,name) void

#define THIS_MODULE ((struct module *)0)

#endif /* _LINUX_MODULE_H */
