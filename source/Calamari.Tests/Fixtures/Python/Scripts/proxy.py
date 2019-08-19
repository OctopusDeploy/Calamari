import os

print('HTTP_PROXY:' + os.environ.get('HTTP_PROXY', ''))
print('HTTPS_PROXY:' + os.environ.get('HTTPS_PROXY', ''))
print('NO_PROXY:' + os.environ.get('NO_PROXY', ''))
