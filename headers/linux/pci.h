#ifndef LINUX_PCI_H
#define LINUX_PCI_H

#include <linux/mod_devicetable.h>
#include <linux/types.h>
#include <linux/init.h>
#include <linux/compiler.h>
#include <linux/errno.h>
#include <linux/atomic.h>
#include <linux/device.h>
#include <linux/pci_ids.h>
#include <linux/pci_regs.h>
#include <linux/spinlock.h>
#include <linux/string.h>
#include <linux/list.h>

#define DEVICE_COUNT_RESOURCE	11

typedef int pci_power_t;

#define PCI_D0 ((pci_power_t) 0)
#define PCI_D1 ((pci_power_t) 1)
#define PCI_D2 ((pci_power_t) 2)
#define PCI_D3hot ((pci_power_t) 3)
#define PCI_D3cold ((pci_power_t) 4)
#define PCI_UNKNOWN ((pci_power_t) 5)
#define PCI_POWER_ERROR	((pci_power_t) -1)

struct pci_dev {
	struct pci_bus *bus;

  unsigned int devfn;
  unsigned short vendor;
  unsigned short device;

  u64 dma_mask;

  struct device dev;
  
  unsigned int irq;
  struct resource resource[DEVICE_COUNT_RESOURCE];
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
	int (*probe) (struct pci_dev *dev, const struct pci_device_id *id);
	void (*remove) (struct pci_dev *dev);
	int (*suspend) (struct pci_dev *dev, pm_message_t state);
	int (*suspend_late) (struct pci_dev *dev, pm_message_t state);
	int (*resume_early) (struct pci_dev *dev);
	int (*resume) (struct pci_dev *dev);
	void (*shutdown) (struct pci_dev *dev);
	int (*sriov_configure) (struct pci_dev *dev, int num_vfs);
	const struct pci_error_handlers *err_handler;
	struct device_driver driver;
	struct pci_dynids dynids;
};

int pci_register_driver(struct pci_driver *);
void pci_unregister_driver(struct pci_driver *dev);

void pci_clear_master(struct pci_dev *dev);
int pci_wake_from_d3(struct pci_dev *dev, bool enable);
int pci_set_power_state(struct pci_dev *dev, pci_power_t state);

static inline const char *pci_name(const struct pci_dev *pdev)
{
	return dev_name(&pdev->dev);
}

static inline void *pci_get_drvdata(struct pci_dev *pdev)
{
	return dev_get_drvdata(&pdev->dev);
}

static inline void pci_set_drvdata(struct pci_dev *pdev, void *data)
{
	dev_set_drvdata(&pdev->dev, data);
}

#define	to_pci_dev(n) container_of(n, struct pci_dev, dev)

#define DEFINE_PCI_DEVICE_TABLE(_table) const struct pci_device_id _table[]

#define module_pci_driver(__pci_driver) module_driver(__pci_driver, pci_register_driver, pci_unregister_driver)

#define PCI_DEVFN(slot, func)	((((slot) & 0x1f) << 3) | ((func) & 0x07))
#define PCI_SLOT(devfn)		(((devfn) >> 3) & 0x1f)
#define PCI_FUNC(devfn)		((devfn) & 0x07)

#define PCIIOC_BASE		('P' << 24 | 'C' << 16 | 'I' << 8)
#define PCIIOC_CONTROLLER	(PCIIOC_BASE | 0x00)
#define PCIIOC_MMAP_IS_IO	(PCIIOC_BASE | 0x01)
#define PCIIOC_MMAP_IS_MEM	(PCIIOC_BASE | 0x02)
#define PCIIOC_WRITE_COMBINE	(PCIIOC_BASE | 0x03)

#endif /* LINUX_PCI_H */
