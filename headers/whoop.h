//
// Whoop header files and definitions
//

struct test_driver {
	int (*probe) (struct device *dev);
	int  (*ep1) (struct net_device *dev);
	int  (*ep2) (struct net_device *dev);
	int  (*ep3) (struct net_device *dev);
	int  (*ep4) (struct net_device *dev);
	int  (*ep5) (struct net_device *dev);
};
