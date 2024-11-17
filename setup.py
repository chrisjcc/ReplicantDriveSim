from setuptools import setup, find_namespace_packages

setup(
    packages=find_namespace_packages(
        include=['replicantdrivesim', 'replicantdrivesim.rl']
    ),
    package_data={
        'replicantdrivesim': [
            '*.so',
            'configs/*.yaml',
            'Builds/StandaloneOSX/**/*',
            'Builds/Windows/**/*',
            'Builds/Linux/**/*'
        ]
    },
    include_package_data=True,
    zip_safe=False,
)
