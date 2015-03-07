#ifndef _LINUX_FS_H
#define _LINUX_FS_H

/*
 * This file has definitions for some important file table
 * structures etc.
 */

#include <linux/ioctl.h>
#include <linux/types.h>
#include <linux/sched.h>
#include <asm/atomic.h>
#include <asm/byteorder.h>
#include <linux/err.h>

#undef NR_OPEN
#define NR_OPEN (1024*1024)	/* Absolute upper limit on fd num */
#define INR_OPEN 1024		/* Initial setting for nfile rlimits */

#define BLOCK_SIZE_BITS 10
#define BLOCK_SIZE (1<<BLOCK_SIZE_BITS)

#define SEEK_SET	0	/* seek relative to beginning of file */
#define SEEK_CUR	1	/* seek relative to current file position */
#define SEEK_END	2	/* seek relative to end of file */

#define NR_FILE  8192	/* this can well be larger on a larger system */

#define MAY_EXEC 1
#define MAY_WRITE 2
#define MAY_READ 4
#define MAY_APPEND 8

#define FMODE_READ 1
#define FMODE_WRITE 2

/* Internal kernel extensions */
#define FMODE_LSEEK	4
#define FMODE_PREAD	8
#define FMODE_PWRITE	FMODE_PREAD	/* These go hand in hand */

#define RW_MASK		1
#define RWA_MASK	2
#define READ 0
#define WRITE 1
#define READA 2		/* read-ahead  - don't block if no resources */
#define SWRITE 3	/* for ll_rw_block() - wait for buffer lock */
#define SPECIAL 4	/* For non-blockdevice requests in request queue */
#define READ_SYNC	(READ | (1 << BIO_RW_SYNC))
#define WRITE_SYNC	(WRITE | (1 << BIO_RW_SYNC))
#define WRITE_BARRIER	((1 << BIO_RW) | (1 << BIO_RW_BARRIER))

#define BLKROSET   _IO(0x12,93)	/* set device read-only (0 = read-write) */
#define BLKROGET   _IO(0x12,94)	/* get read-only status (0 = read_write) */
#define BLKRRPART  _IO(0x12,95)	/* re-read partition table */
#define BLKGETSIZE _IO(0x12,96)	/* return device size /512 (long *arg) */
#define BLKFLSBUF  _IO(0x12,97)	/* flush buffer cache */
#define BLKRASET   _IO(0x12,98)	/* set read ahead for block device */
#define BLKRAGET   _IO(0x12,99)	/* get current read ahead setting */
#define BLKFRASET  _IO(0x12,100)/* set filesystem (mm/filemap.c) read-ahead */
#define BLKFRAGET  _IO(0x12,101)/* get filesystem (mm/filemap.c) read-ahead */
#define BLKSECTSET _IO(0x12,102)/* set max sectors per request (ll_rw_blk.c) */
#define BLKSECTGET _IO(0x12,103)/* get max sectors per request (ll_rw_blk.c) */
#define BLKSSZGET  _IO(0x12,104)/* get block device sector size */

struct hd_geometry;
struct iovec;
struct poll_table_struct;
struct vm_area_struct;

struct page;
struct seq_file;

struct address_space {
    struct inode		*host;		/* owner: inode, block_device */
};

struct file {
    struct dentry		*f_dentry;
    struct file_operations	*f_op;
    atomic_t		        f_count;
    unsigned int 	        f_flags;
    mode_t		        f_mode;
    loff_t		        f_pos;
    void                        *private_data;
    struct address_space	*f_mapping;
};

struct block_device {
    struct inode *		bd_inode;	/* will die */
    struct gendisk *	        bd_disk;

    struct block_device *	bd_contains;
    unsigned		        bd_block_size;
};

struct inode {
    umode_t			i_mode;

    struct block_device	 *i_bdev;
    dev_t		 i_rdev;
    loff_t		 i_size;
    struct cdev	         *i_cdev;
};

typedef struct {
	size_t written;
	size_t count;
} read_descriptor_t;

typedef int (*filldir_t)(void *, const char *, int, loff_t, ino_t, unsigned);
typedef int (*read_actor_t)(read_descriptor_t *, struct page *, unsigned long, unsigned long);

struct file_lock {
    int something;
};

struct file_operations {
    struct module *owner;
    loff_t (*llseek) (struct file *, loff_t, int);
    ssize_t (*read) (struct file *, char __user *, size_t, loff_t *);
    ssize_t (*write) (struct file *, const char __user *, size_t, loff_t *);
    int (*readdir) (struct file *, void *, filldir_t);
    unsigned int (*poll) (struct file *, struct poll_table_struct *);
    int (*ioctl) (struct inode *, struct file *, unsigned int, unsigned long);
    long (*unlocked_ioctl) (struct file *, unsigned int, unsigned long);
    long (*compat_ioctl) (struct file *, unsigned int, unsigned long);
    int (*mmap) (struct file *, struct vm_area_struct *);
    int (*open) (struct inode *, struct file *);
    int (*flush) (struct file *);
    int (*release) (struct inode *, struct file *);
    int (*fsync) (struct file *, struct dentry *, int datasync);
    int (*fasync) (int, struct file *, int);
    int (*lock) (struct file *, int, struct file_lock *);
    ssize_t (*readv) (struct file *, const struct iovec *, unsigned long, loff_t *);
    ssize_t (*writev) (struct file *, const struct iovec *, unsigned long, loff_t *);
    ssize_t (*sendfile) (struct file *, loff_t *, size_t, read_actor_t, void *);
    ssize_t (*sendpage) (struct file *, struct page *, int, size_t, loff_t *, int);
    unsigned long (*get_unmapped_area)(struct file *, unsigned long, unsigned long, unsigned long, unsigned long);
    int (*check_flags)(int);
    int (*dir_notify)(struct file *filp, unsigned long arg);
    int (*flock) (struct file *, int, struct file_lock *);
    int (*open_exec) (struct inode *);
};

struct block_device_operations {
	int (*open) (struct inode *, struct file *);
	int (*release) (struct inode *, struct file *);
	int (*ioctl) (struct inode *, struct file *, unsigned, unsigned long);
	long (*unlocked_ioctl) (struct file *, unsigned, unsigned long);
	long (*compat_ioctl) (struct file *, unsigned, unsigned long);
	int (*direct_access) (struct block_device *, sector_t, unsigned long *);
	int (*media_changed) (struct gendisk *);
	int (*revalidate_disk) (struct gendisk *);
	int (*getgeo)(struct block_device *, struct hd_geometry *);
	struct module *owner;
};


struct fasync_struct {
    int something;
};

extern int fasync_helper(int, struct file *, int, struct fasync_struct **);


/*
 * return data direction, READ or WRITE
 */
#define bio_data_dir(bio)	((bio)->bi_rw & 1)


int alloc_chrdev_region(dev_t *, unsigned, unsigned, const char *);
int register_chrdev_region(dev_t, unsigned, const char *);
void unregister_chrdev_region(dev_t, unsigned);

int register_chrdev(unsigned int, const char *, struct file_operations *);
int unregister_chrdev(unsigned int, const char *);

int chrdev_open(struct inode *, struct file *);
void chrdev_show(struct seq_file *,off_t);

#define BDEVNAME_SIZE	32	/* Largest string for a blockdev identifier */

int register_blkdev(unsigned int, const char *);
int unregister_blkdev(unsigned int, const char *);


void kill_fasync(struct fasync_struct **, int, int);

static inline unsigned iminor(struct inode *inode)
{
	return MINOR(inode->i_rdev);
}

static inline unsigned imajor(struct inode *inode)
{
	return MAJOR(inode->i_rdev);
}

loff_t no_llseek(struct file *file, loff_t offset, int origin);

int check_disk_change(struct block_device *);

/*
 * return READ, READA, or WRITE
 */
#define bio_rw(bio)		((bio)->bi_rw & (RW_MASK | RWA_MASK))

int nonseekable_open(struct inode * inode, struct file * filp);

loff_t i_size_read(struct inode *inode);

int set_blocksize(struct block_device *, int);
#endif
