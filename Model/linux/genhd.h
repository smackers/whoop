#ifndef _LINUX_GENHD_H
#define _LINUX_GENHD_H

#include <linux/types.h>
#include <linux/major.h>
#include <linux/device.h>
//#include <linux/smp.h>
#include <linux/string.h>
#include <linux/fs.h>

#define GENHD_FL_REMOVABLE			1
#define GENHD_FL_DRIVERFS			2
#define GENHD_FL_CD				8
#define GENHD_FL_UP				16
#define GENHD_FL_SUPPRESS_PARTITION_INFO	32

struct gendisk {
	int major;			/* major number of driver */
	int first_minor;
	int minors;                     /* maximum number of minors, =1 for
                                         * disks that can't be partitioned. */
	char disk_name[32];		/* name of major driver */
	struct block_device_operations *fops;
	struct request_queue *queue;
	void *private_data;

	int flags;
	struct device *driverfs_dev;
	char devfs_name[64];		/* devfs crap */
};

// DDV: defined in file linux/block/genhd.c
void add_disk(struct gendisk *disk);
// DDV: defined in file linux/block/genhd.c
void del_gendisk(struct gendisk *gp);
// DDV: defined in file linux/block/genhd.c
struct gendisk *alloc_disk(int minors);
// DDV: TODO
void put_disk(struct gendisk *disk);
// DDV: TODO
extern struct kobject *get_disk(struct gendisk *disk);

// DDV: TODO
void set_capacity(struct gendisk *disk, sector_t size);

// DDV: TODO
void add_disk_randomness(struct gendisk *disk);
#endif
