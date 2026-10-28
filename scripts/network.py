import ipaddress
import socket


def _usable_address(address):
    try:
        parsed_address = ipaddress.ip_address(address)
    except ValueError:
        return False
    if parsed_address.is_loopback:
        return False
    if parsed_address.is_unspecified:
        return False
    if parsed_address.is_link_local:
        return False
    return True


def _default_route_address():
    network_socket = None
    try:
        # A UDP connect selects the default interface without sending a packet.
        network_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        network_socket.connect(("8.8.8.8", 80))
        address = network_socket.getsockname()[0]
        if _usable_address(address):
            return address
    except OSError:
        return ""
    finally:
        if network_socket:
            network_socket.close()
    return ""


def _fallback_address():
    addresses = set()
    try:
        host_name = socket.gethostname()
        records = socket.getaddrinfo(host_name, None, socket.AF_INET)
    except OSError:
        return ""

    for record in records:
        address = record[4][0]
        if _usable_address(address):
            addresses.add(address)
    sorted_addresses = sorted(addresses)
    if sorted_addresses:
        return sorted_addresses[0]
    return ""


def get_lan_addresses():
    address = _default_route_address()
    if not address:
        address = _fallback_address()
    if address:
        return [address]
    return []
