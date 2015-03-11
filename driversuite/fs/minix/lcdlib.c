#include <linux/fs.h>
#include <linux/buffer_head.h>
#include <linux/dcache.h>
#include <linux/printk.h>
#include <linux/rcupdate.h>
#include <linux/rcutree.h>
#include <linux/sched.h>
#include <linux/rwlock_api_smp.h>
#include <linux/spinlock_api_smp.h>
#include <linux/highuid.h>
#include <linux/time.h>
#include <linux/pagemap.h>
#include <linux/mm.h>
#include <linux/slab.h>
#include <asm-generic/bitops/find.h>
#include <asm-generic/bitops/ffz.h>

/*
 * To be filled by stubs.
 */

void __fentry__(void)
{
}

/* include/linux/fs.h */

const char *bdevname(struct block_device *bdev, char *buffer)
{
	return NULL;
}

int inode_change_ok(const struct inode *inode, struct iattr *attr)
{
	return 0;
}

int inode_newsize_ok(const struct inode *inode, loff_t offset)
{
	return 0;
}

void setattr_copy(struct inode *inode, const struct iattr *attr)
{
}

void iget_failed(struct inode *inode)
{
}

int sb_set_blocksize(struct super_block *inode, int size)
{
	return 0;
}

int register_filesystem(struct file_system_type *fs)
{
	return 0;
}

int unregister_filesystem(struct file_system_type *fs)
{	
	return 0;
}

void __mark_inode_dirty(struct inode *inode, int flag)
{
}

void clear_inode(struct inode *inode)
{
}

void drop_nlink(struct inode *inode)
{
}

void inc_nlink(struct inode *inode)
{
}

struct inode * iget_locked(struct super_block *sb, unsigned long flag)
{
	return NULL;
}

void ihold(struct inode * inode)
{
}

void iput(struct inode *inode)
{
}

void init_special_inode(struct inode *inode, umode_t mode, dev_t dev)
{
}

void inode_init_once(struct inode *inode)
{
}

void inode_init_owner(struct inode *inode, const struct inode *dir,
		      umode_t mode)
{
}

void __insert_inode_hash(struct inode *inode, unsigned long hashval)
{
}

struct inode *new_inode(struct super_block *sb)
{
	return NULL;
}

void set_nlink(struct inode *inode, unsigned int nlink)
{
}

void unlock_new_inode(struct inode *inode)
{
}

int generic_file_fsync(struct file *flip, loff_t oftsrc, loff_t oftdst, int flag)
{
	return 0;
}

ssize_t generic_read_dir(struct file *filp, char __user *buf, size_t size, loff_t *oft)
{
	return 0;
}

int generic_readlink(struct dentry *entry, char __user *buf, int flag)
{
	return 0;
}

void *page_follow_link_light(struct dentry *entry, struct nameidata *ni)
{
}

void page_put_link(struct dentry *entry, struct nameidata *ni, void *buf)
{
}

int page_symlink(struct inode *inode, const char *symname, int len)
{
	return 0;
}

ssize_t do_sync_read(struct file *filp, char __user *buf, size_t len, loff_t *ppos)
{
	return 0;
}

ssize_t do_sync_write(struct file *filp, const char __user *buf, size_t len, loff_t *ppos)
{
	return 0;
}

loff_t generic_file_llseek(struct file *file, loff_t offset, int whence)
{
	return 0;
}

ssize_t generic_file_splice_read(struct file *flip, loff_t *oft,
				 struct pipe_inode_info *pinfo, size_t size, unsigned int flag)
{
	return 0;
}

void generic_fillattr(struct inode *inode, struct kstat *stat)
{
}

void kill_block_super(struct super_block *sb)
{
}

struct dentry *mount_bdev(struct file_system_type *fs_type,
			  int flags, const char *dev_name, void *data,
			  int (*fill_super)(struct super_block *, void *, int))
{
	return NULL;
}

ssize_t generic_file_aio_read(struct kiocb *iocb, const struct iovec *vec, unsigned long size, loff_t oft)
{
	return 0;
}

ssize_t generic_file_aio_write(struct kiocb *iocb, const struct iovec *vec, unsigned long size, loff_t oft)
{
	return 0;
}

int generic_file_mmap(struct file *flip, struct vm_area_struct *vma)
{
	return 0;
}


/* include/linux/buffer_head.h */

void __lock_buffer(struct buffer_head *bh)
{
}

void mark_buffer_dirty(struct buffer_head *bh)
{
}

void mark_buffer_dirty_inode(struct buffer_head *bh, struct inode *inode)
{
}

int sync_dirty_buffer(struct buffer_head *bh)
{
	return 0;
}

void __bforget(struct buffer_head *bh)
{
}

int block_read_full_page(struct page *p, get_block_t* gb)
{
	return 0;
}

int block_truncate_page(struct address_space *as, loff_t oft, get_block_t *gb)
{
	return 0;
}

int block_write_begin(struct address_space *mapping, loff_t pos, unsigned len,
		      unsigned flags, struct page **pagep, get_block_t *get_block)
{
	return 0;
}

int __block_write_begin(struct page *page, loff_t pos, unsigned len,
			get_block_t *get_block)
{
	return 0;
}

int block_write_end(struct file *flip, struct address_space *as,
		    loff_t oft, unsigned pos, unsigned size,
		    struct page *p, void *buf)
{
	return 0;
}

int block_write_full_page(struct page *page, get_block_t *get_block,
			  struct writeback_control *wbc)
{
	return 0;
}

struct buffer_head *__bread(struct block_device *bdev, sector_t block, unsigned size)
{
	return NULL;
}

void __brelse(struct buffer_head *bh)
{
}

sector_t generic_block_bmap(struct address_space *as, sector_t sec, get_block_t *gb)
{
	return 0;
}

int generic_write_end(struct file *flip, struct address_space *as,
		      loff_t oft, unsigned pos, unsigned flag,
		      struct page *p, void *buf)
{
	return 0;
}

struct buffer_head *__getblk(struct block_device *bdev, sector_t block,
			     unsigned size)
{
	return NULL;
}

inline void invalidate_inode_buffers(struct inode *inode)
{
}

void unlock_buffer(struct buffer_head *bh)
{
}


/* include/linux/dcache.h */

void d_instantiate(struct dentry *entry, struct inode *inode)
{
}

struct dentry * d_make_root(struct inode *inode)
{
	return NULL;
}

void d_rehash(struct dentry *entry)
{
}

/* GCC -fstack-protector */
void __stack_chk_fail(void)
{
}

/* include/linux/printk.h */

int printk(const char *s, ...)
{
	return 0;
}

int __printk_ratelimit(const char *func)
{
	return 0;
}

/*int printk_ratelimit(void)
{
	return 0;
	}*/


/* include/linux/rcuupdate.h */
void call_rcu(struct rcu_head *head,
	      void (*func)(struct rcu_head *head))
{
}

/* include/linux/rcutree.h */
void rcu_barrier(void)
{
}


/* include/linux/sched.h */
int _cond_resched(void)
{
}

/* include/linux/highuid.h */
int fs_overflowuid = DEFAULT_FS_OVERFLOWUID; /* 65534 */
int fs_overflowgid = DEFAULT_FS_OVERFLOWUID;

/* include/linux/time.h */
unsigned long get_seconds(void)
{
	return 0;
}

/* include/linux/pagemap.h */
struct page * find_or_create_page(struct address_space *mapping,
				  pgoff_t index, gfp_t gfp_mask)
{
	return NULL;
}

void __lock_page(struct page *page)
{
}

void unlock_page(struct page *page)
{
}

struct page * read_cache_page(struct address_space *mapping,
			      pgoff_t index, filler_t *filler, void *data)
{
	return NULL;
}

/* include/linux/mm.h */
int write_one_page(struct page *page, int wait)
{
	return 0;
}

void put_page(struct page *page)
{
}

void truncate_pagecache(struct inode *inode, loff_t old, loff_t new)
{
}

void truncate_setsize(struct inode *inode, loff_t newsize)
{
}

int truncate_inode_page(struct address_space *mapping, struct page *page)
{
	return 0;
}

void truncate_inode_pages(struct address_space *a, loff_t oft)
{
}


/* include/linux/slab.h */
struct kmem_cache *kmalloc_caches[KMALLOC_SHIFT_HIGH + 1];

void kfree(const void *buf)
{
}

void *__kmalloc(size_t size, gfp_t flags)
{
	return NULL;
}

void *kmem_cache_alloc(struct kmem_cache *s, gfp_t flags)
{
	return NULL;
}

void *kmem_cache_alloc_trace(struct kmem_cache *s, gfp_t gfpflags, size_t size)
{
	return NULL;
}

struct kmem_cache *kmem_cache_create(const char *b, size_t s, size_t nr,
				     unsigned long sz,
				     void (*op)(void *))
{
	return NULL;
}

void kmem_cache_destroy(struct kmem_cache *m)
{
}

void kmem_cache_free(struct kmem_cache *m, void *obj)
{
}

/*
 * Implementations
 */


/* include/linux/rwlock_api_smp.h */

#include <asm/spinlock.h>
void __lockfunc _raw_read_lock(rwlock_t *lock)
{
	arch_read_lock(&lock->raw_lock);
}

void __lockfunc _raw_write_lock(rwlock_t *lock)
{
	arch_write_lock(&lock->raw_lock);
}

/* include/linux/spinlock_api_smp.h */
void __lockfunc _raw_spin_lock(raw_spinlock_t *lock)
{
	arch_spin_lock(&lock->raw_lock);
}

/* include/asm-generic/bitops/find.h */
/* ffz in asm-generic/bitops/ffz.h */
unsigned long find_first_zero_bit(const unsigned long *addr, unsigned long size)
{
	const unsigned long *p = addr;
	unsigned long result = 0;
	unsigned long tmp;

	while (size & ~(BITS_PER_LONG-1)) {
		if (~(tmp = *(p++)))
			goto found;
		result += BITS_PER_LONG;
		size -= BITS_PER_LONG;
	}
	if (!size)
		return result;

	tmp = (*p) | (~0UL << size);
	if (tmp == ~0UL)	/* Are any bits zero? */
		return result + size;	/* Nope. */
found:
	return result + ffz(tmp);
}

/* include/linux/bitops.h */
unsigned int __sw_hweight32(unsigned int w)
{
#ifdef ARCH_HAS_FAST_MULTIPLIER
	w -= (w >> 1) & 0x55555555;
	w =  (w & 0x33333333) + ((w >> 2) & 0x33333333);
	w =  (w + (w >> 4)) & 0x0f0f0f0f;
	return (w * 0x01010101) >> 24;
#else
	unsigned int res = w - ((w >> 1) & 0x55555555);
	res = (res & 0x33333333) + ((res >> 2) & 0x33333333);
	res = (res + (res >> 4)) & 0x0F0F0F0F;
	res = res + (res >> 8);
	return (res + (res >> 16)) & 0x000000FF;
#endif
}

/* include/linux/string.h */
void *memcpy(void *dest, const void *src, size_t count)
{
	char *tmp = dest;
	const char *s = src;

	while (count--)
		*tmp++ = *s++;
	return dest;
}

int memcmp(const void *cs, const void *ct, size_t count)
{
	const unsigned char *su1, *su2;
	int res = 0;

	for (su1 = cs, su2 = ct; 0 < count; ++su1, ++su2, count--)
		if ((res = *su1 - *su2) != 0)
			break;
	return res;
}

void *memset(void *s, int c, size_t count)
{
	char *xs = s;

	while (count--)
		*xs++ = c;
	return s;
}

size_t strlen(const char *s)
{
	const char *sc;

	for (sc = s; *sc != '\0'; ++sc)
		/* nothing */;
	return sc - s;
}

size_t strnlen(const char *s, size_t count)
{
	const char *sc;

	for (sc = s; count-- && *sc != '\0'; ++sc)
		/* nothing */;
	return sc - s;
}
