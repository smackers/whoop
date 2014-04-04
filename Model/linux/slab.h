#ifndef _LINUX_SLAB_H
#define	_LINUX_SLAB_H

unsigned int ksize(const void *);

void *kmalloc(size_t size, gfp_t flags)
{
	void *memory = (void *) malloc(size);
	return memory;
}

void *kzalloc(size_t size, gfp_t flags)
{
	void *memory = (void *) malloc(size);
	return memory;
}

void *kmalloc_node(size_t size, gfp_t flags, int node)
{
	void *memory = (void *) malloc(size);
	return memory;
}

void kfree(const void *);

#endif
