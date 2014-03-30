#ifndef _LINUX_SLAB_H
#define	_LINUX_SLAB_H

unsigned int ksize(const void *);

void *kmalloc(size_t size, gfp_t flags);

void *kzalloc(size_t size, gfp_t flags);

void *__kmalloc_node(size_t size, gfp_t flags, int node);

void kfree(const void *);

#endif
