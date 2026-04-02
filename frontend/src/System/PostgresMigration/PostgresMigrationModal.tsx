import React from 'react';
import Modal from 'Components/Modal/Modal';
import PostgresMigrationModalContent, {
  PostgresMigrationModalContentProps,
} from './PostgresMigrationModalContent';

interface PostgresMigrationModalProps
  extends PostgresMigrationModalContentProps {
  isOpen: boolean;
}

function PostgresMigrationModal({
  isOpen,
  onModalClose,
  ...otherProps
}: PostgresMigrationModalProps) {
  return (
    <Modal isOpen={isOpen} size="large" onModalClose={onModalClose}>
      <PostgresMigrationModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default PostgresMigrationModal;
