.. ReplicantDriveSim documentation master file

Welcome to ReplicantDriveSim's documentation!
=============================================

.. image:: https://github.com/chrisjcc/ReplicantDriveSim/actions/workflows/publish-gh-pages.yml/badge.svg?branch=main
   :alt: Workflow Status
   :target: https://github.com/chrisjcc/ReplicantDriveSim/actions/workflows/publish-gh-pages.yml

ReplicantDriveSim is an advanced traffic simulation project designed for autonomous driving research. It leverages reinforcement learning, imitation learning, and computer vision to create realistic traffic scenarios and synthetic driving data. The simulation environment is built using Pygame for visualization and Miniforge for Python package management, ensuring a seamless development and deployment experience.

.. image:: https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/NISSAN-GTR_ReplicantDriveSim.png
   :alt: Nissan GTR

Quick Links
-----------

* `Traffic Simulation Documentation <https://chrisjcc.github.io/ReplicantDriveSim/>`_
* `Doxygen Documentation <https://chrisjcc.github.io/ReplicantDriveSim/External/docs/html/>`_
* `AI Page <https://chrisjcc.github.io/ReplicantDriveSim/rl/>`_
* `GitHub Page <https://github.com/chrisjcc/ReplicantDriveSim/>`_

Project Setup
-------------

Required Unity Version
^^^^^^^^^^^^^^^^^^^^^^

This project was developed using Unity 2022.3.39f1 (LTS). To ensure compatibility, please use this version or later Long-Term Support (LTS) versions of Unity.

Installation
^^^^^^^^^^^^

1. Clone the repository:

   .. code-block:: shell

      git clone git@github.com:chrisjcc/ReplicantDriveSim.git

2. Open the project in Unity Hub and select Unity version 2022.3.39f1.
3. Let Unity install any necessary packages and dependencies.

Generate Doxygen Documentation
------------------------------

Prerequisites
^^^^^^^^^^^^^

Ensure Doxygen is installed on your local machine:

- Ubuntu:

  .. code-block:: bash

     sudo apt-get install doxygen
     sudo apt-get install graphviz

- macOS:

  .. code-block:: bash

     brew install doxygen
     brew install graphviz

Generate Documentation
^^^^^^^^^^^^^^^^^^^^^^

1. Navigate to the root directory of your project where the Doxyfile is located.
2. Run the Doxygen command:

   .. code-block:: bash

      doxygen Doxyfile

3. The generated HTML files can be found in the directory specified by the OUTPUT_DIRECTORY setting in the Doxyfile (typically docs/html).

.. toctree::
   :maxdepth: 2
   :caption: Contents:

   module1
   module2

Indices and tables
==================

* :ref:`genindex`
* :ref:`modindex`
* :ref:`search`
