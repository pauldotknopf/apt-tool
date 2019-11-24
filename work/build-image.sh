#!/usr/bin/env bash
set -ex

function test-command() {
    command -v $1 >/dev/null 2>&1 || { echo >&2 "The command \"$1\" is required.  Try \"apt-get install $2\"."; exit 1; }
}

test-command arch-chroot arch-install-scripts
test-command genfstab arch-install-scripts
test-command parted parted

# Create our hard disk
rm -rf boot.img
truncate -s 20G boot.img
parted -s boot.img \
        mklabel msdos \
        mkpart primary 0% 100%

# Mount the newly created drive
loop_device=`losetup --partscan --show --find boot.img`

# Format the partitions
mkfs.ext4 ${loop_device}p1

# Mount the new partitions
rm -rf rootfs-mounted && mkdir rootfs-mounted
mount ${loop_device}p1 rootfs-mounted
cp -a rootfs/* rootfs-mounted

# Set the computer name
echo "demo" > rootfs-mounted/etc/hostname

# Install GRUB
arch-chroot rootfs-mounted grub-install ${loop_device}
arch-chroot rootfs-mounted grub-mkconfig -o /boot/grub/grub.cfg
genfstab -U -p rootfs-mounted | sed -e 's/#.*$//' -e '/^$/d' > rootfs-mounted/etc/fstab

# Clean up
umount rootfs-mounted
rm -r rootfs-mounted
losetup -d ${loop_device}

qemu-img convert -O vmdk boot.img boot.vmdk