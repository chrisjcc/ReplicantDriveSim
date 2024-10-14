import replicantdrivesim

print("Imported replicantdrivesim")
print("Available attributes:", dir(replicantdrivesim))

try:
    env = replicantdrivesim.make("replicantdrivesim-v0")
    print("Successfully created environment")
except AttributeError as e:
    print(f"AttributeError: {e}")
except Exception as e:
    print(f"Unexpected error: {e}")

# Additional checks
print("Is 'make' in dir(replicantdrivesim)?", 'make' in dir(replicantdrivesim))
print("Type of replicantdrivesim:", type(replicantdrivesim))
print("replicantdrivesim.__file__:", replicantdrivesim.__file__)
