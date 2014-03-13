#ifndef LINUX_PCI_H
#define LINUX_PCI_H

#include <linux/mod_devicetable.h>
#include <linux/types.h>
#include <linux/init.h>
#include <linux/compiler.h>
#include <linux/atomic.h>
#include <linux/device.h>
#include <linux/pci_ids.h>
#include <linux/pci_regs.h>
#include <linux/spinlock.h>

struct pci_dev {
	void *data;
};

struct pci_dynids {
	spinlock_t lock;
	struct list_head list;
};

struct module;
struct pci_driver {
	struct list_head node;
	const char *name;
	const struct pci_device_id *id_table;
	int  (*probe)  (struct pci_dev *dev, const struct pci_device_id *id);
	void (*remove) (struct pci_dev *dev);
	int  (*suspend) (struct pci_dev *dev, pm_message_t state);
	int  (*suspend_late) (struct pci_dev *dev, pm_message_t state);
	int  (*resume_early) (struct pci_dev *dev);
	int  (*resume) (struct pci_dev *dev);
	void (*shutdown) (struct pci_dev *dev);
	int (*sriov_configure) (struct pci_dev *dev, int num_vfs);
	const struct pci_error_handlers *err_handler;
	struct device_driver	driver;
	struct pci_dynids dynids;
};

#define DEFINE_PCI_DEVICE_TABLE(_table) const struct pci_device_id _table[]

int pci_register_driver(struct pci_driver *);
void pci_unregister_driver(struct pci_driver *dev);

#define module_pci_driver(__pci_driver) \
	module_driver(__pci_driver, pci_register_driver, \
		       pci_unregister_driver)

#define PCI_DEVFN(slot, func)	((((slot) & 0x1f) << 3) | ((func) & 0x07))
#define PCI_SLOT(devfn)		(((devfn) >> 3) & 0x1f)
#define PCI_FUNC(devfn)		((devfn) & 0x07)

#define PCIIOC_BASE		('P' << 24 | 'C' << 16 | 'I' << 8)
#define PCIIOC_CONTROLLER	(PCIIOC_BASE | 0x00)
#define PCIIOC_MMAP_IS_IO	(PCIIOC_BASE | 0x01)
#define PCIIOC_MMAP_IS_MEM	(PCIIOC_BASE | 0x02)
#define PCIIOC_WRITE_COMBINE	(PCIIOC_BASE | 0x03)

#endif /* LINUX_PCI_H */
