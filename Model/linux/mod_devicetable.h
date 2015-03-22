#ifndef LINUX_MOD_DEVICETABLE_H
#define LINUX_MOD_DEVICETABLE_H

#include <linux/types.h>

typedef unsigned long kernel_ulong_t;

#define PCI_ANY_ID (~0)

#define I2C_NAME_SIZE   20

#define IEEE1394_MATCH_VENDOR_ID	0x0001
#define IEEE1394_MATCH_MODEL_ID		0x0002
#define IEEE1394_MATCH_SPECIFIER_ID	0x0004
#define IEEE1394_MATCH_VERSION		0x0008

#define CCW_DEVICE_ID_MATCH_CU_TYPE             0x01
#define CCW_DEVICE_ID_MATCH_CU_MODEL            0x02
#define CCW_DEVICE_ID_MATCH_DEVICE_TYPE         0x04
#define CCW_DEVICE_ID_MATCH_DEVICE_MODEL        0x08

struct pci_device_id {
	__u32 vendor, device;		/* Vendor and device ID or PCI_ANY_ID*/
	__u32 subvendor, subdevice;	/* Subsystem ID's or PCI_ANY_ID */
	__u32 class, class_mask;	/* (class,subclass,prog-if) triplet */
	kernel_ulong_t driver_data;	/* Data private to the driver */
};

struct ieee1394_device_id {
	__u32 match_flags;
	__u32 vendor_id;
	__u32 model_id;
	__u32 specifier_id;
	__u32 version;
	kernel_ulong_t driver_data;
};

struct i2c_device_id {
	char name[I2C_NAME_SIZE];
	kernel_ulong_t driver_data;     /* Data private to the driver */
};


/* dmi */
enum dmi_field {
	DMI_NONE,
	DMI_BIOS_VENDOR,
	DMI_BIOS_VERSION,
	DMI_BIOS_DATE,
	DMI_SYS_VENDOR,
	DMI_PRODUCT_NAME,
	DMI_PRODUCT_VERSION,
	DMI_PRODUCT_SERIAL,
	DMI_PRODUCT_UUID,
	DMI_BOARD_VENDOR,
	DMI_BOARD_NAME,
	DMI_BOARD_VERSION,
	DMI_BOARD_SERIAL,
	DMI_BOARD_ASSET_TAG,
	DMI_CHASSIS_VENDOR,
	DMI_CHASSIS_TYPE,
	DMI_CHASSIS_VERSION,
	DMI_CHASSIS_SERIAL,
	DMI_CHASSIS_ASSET_TAG,
	DMI_STRING_MAX,
};

struct dmi_strmatch {
	unsigned char slot:7;
	unsigned char exact_match:1;
	char substr[79];
};

struct dmi_system_id {
	int (*callback)(const struct dmi_system_id *);
	const char *ident;
	struct dmi_strmatch matches[4];
	void *driver_data;
};

struct ccw_device_id {
	__u16   match_flags;    /* which fields to match against */
	__u16   cu_type;        /* control unit type     */
	__u16   dev_type;       /* device type           */
	__u8    cu_model;       /* control unit model    */
	__u8    dev_model;      /* device model          */
	kernel_ulong_t driver_info;
};

#define DMI_MATCH(a, b) { .slot = a, .substr = b }

#endif /* LINUX_MOD_DEVICETABLE_H */
