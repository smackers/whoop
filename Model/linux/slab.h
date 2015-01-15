#ifndef _LINUX_SLAB_H
#define	_LINUX_SLAB_H

inline void *kmalloc(size_t size, gfp_t flags);

inline void *kzalloc(size_t size, gfp_t flags);

inline void *kmalloc_node(size_t size, gfp_t flags, int node);

inline unsigned int ksize(const void *);

inline void kfree(const void *);

#endif
