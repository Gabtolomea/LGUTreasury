import ipaddress
import socket


def get_lan_addresses():
    addresses = set()
    try:
        host_name = socket.gethostname()
        records = socket.getaddrinfo(host_name, None, socket.AF_INET)
    except OSError:
        return []

    for record in records:
        address = record[4][0]
        try:
            parsed_address = ipaddress.ip_address(address)
        except ValueError:
            continue
        if parsed_address.is_loopback or parsed_address.is_unspecified or parsed_address.is_link_local:
            continue
        addresses.add(address)
    return sorted(addresses)
