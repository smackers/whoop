#ifndef _LINUX_SKBUFF_H
#define _LINUX_SKBUFF_H

#include <linux/net.h>
#include <linux/slab.h>

#define MAX_SKB_FRAGS 16UL

typedef struct skb_frag_struct skb_frag_t;

struct skb_frag_struct {
	struct {
		struct page *p;
	} page;
	__u16 page_offset;
	__u16 size;
};

static inline unsigned int skb_frag_size(const skb_frag_t *frag)
{
	return frag->size;
}

static inline void skb_frag_size_set(skb_frag_t *frag, unsigned int size)
{
	frag->size = size;
}

static inline void skb_frag_size_add(skb_frag_t *frag, int delta)
{
	frag->size += delta;
}

static inline void skb_frag_size_sub(skb_frag_t *frag, int delta)
{
	frag->size -= delta;
}

typedef unsigned char *sk_buff_data_t;

struct skb_shared_info {
	unsigned char	nr_frags;
	skb_frag_t frags[MAX_SKB_FRAGS];
};

struct sk_buff {
	struct sk_buff *next;
	struct sk_buff *prev;
	
	struct net_device	*dev;
	
	unsigned int len, data_len;
	
	sk_buff_data_t tail;
	sk_buff_data_t end;
	unsigned char *head, *data;
};

static inline unsigned char *skb_end_pointer(const struct sk_buff *skb)
{
	return skb->end;
}

static inline unsigned int skb_end_offset(const struct sk_buff *skb)
{
	return skb->end - skb->head;
}

#define skb_shinfo(SKB)	((struct skb_shared_info *)(skb_end_pointer(SKB)))

static inline unsigned int skb_headlen(const struct sk_buff *skb)
{
	return skb->len - skb->data_len;
}

static inline struct page *skb_frag_page(const skb_frag_t *frag)
{
	return frag->page.p;
}

static inline void *skb_frag_address(const skb_frag_t *frag)
{
	return page_address(skb_frag_page(frag)) + frag->page_offset;
}

void skb_tx_timestamp(struct sk_buff *skb);

#endif	/* _LINUX_SKBUFF_H */
