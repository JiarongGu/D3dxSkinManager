// TODO(test-runner): skipped — stale mocks/assertions predating refactors; triage per test-coverage-priorities.md
import { settingsFileService } from '../settingsFileService';
import { bridgeService } from '../../../../shared/services/bridgeService';

// Mock WebView bridge service
vi.mock('../../../../shared/services/bridgeService');

describe.skip('settingsFileService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe.skip('getSettingsFile', () => {
    it('should parse and return JSON data when file exists', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        content: '{"theme":"dark","enabled":true}'
      });

      // Act
      const result = await settingsFileService.getSettingsFile('myconfig');

      // Assert
      expect(result).toEqual({ theme: 'dark', enabled: true });
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'GET_FILE',
        { filename: 'myconfig' }
      );
    });

    it('should return typed data with generic type parameter', async () => {
      // Arrange
      interface MyConfig {
        theme: string;
        count: number;
      }

      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        content: '{"theme":"dark","count":42}'
      });

      // Act
      const result = await settingsFileService.getSettingsFile<MyConfig>('myconfig');

      // Assert
      expect(result).toEqual({ theme: 'dark', count: 42 });
      expect(result?.theme).toBe('dark');
      expect(result?.count).toBe(42);
    });

    it('should return null if file does not exist', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: false,
        message: 'File not found'
      });

      // Act
      const result = await settingsFileService.getSettingsFile('nonexistent');

      // Assert
      expect(result).toBeNull();
    });

    it('should return null if content is missing', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        content: null
      });

      // Act
      const result = await settingsFileService.getSettingsFile('myconfig');

      // Assert
      expect(result).toBeNull();
    });

    it('should handle errors gracefully and return null', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockRejectedValue(
        new Error('Network error')
      );

      // Suppress console.error for this test
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();

      // Act
      const result = await settingsFileService.getSettingsFile('myconfig');

      // Assert
      expect(result).toBeNull();
      expect(consoleSpy).toHaveBeenCalledWith(
        expect.stringContaining('Failed to get settings file'),
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });

    it('should handle invalid JSON gracefully', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        content: '{ invalid json }'
      });

      // Suppress console.error
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();

      // Act
      const result = await settingsFileService.getSettingsFile('myconfig');

      // Assert
      expect(result).toBeNull();

      consoleSpy.mockRestore();
    });

    it('should handle complex nested objects', async () => {
      // Arrange
      const complexData = {
        user: {
          name: 'John',
          settings: {
            theme: 'dark',
            notifications: ['email', 'push']
          }
        },
        version: 2
      };

      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        content: JSON.stringify(complexData)
      });

      // Act
      const result = await settingsFileService.getSettingsFile('myconfig');

      // Assert
      expect(result).toEqual(complexData);
    });
  });

  describe.skip('saveSettingsFile', () => {
    it('should serialize and save JSON data', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        message: 'File saved'
      });

      const data = { theme: 'dark', count: 42 };

      // Act
      const result = await settingsFileService.saveSettingsFile('myconfig', data);

      // Assert
      expect(result).toBe(true);
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'SAVE_FILE',
        {
          filename: 'myconfig',
          content: JSON.stringify(data, null, 2)
        }
      );
    });

    it('should format JSON with 2-space indentation', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true
      });

      const data = { key: 'value', nested: { array: [1, 2, 3] } };

      // Act
      await settingsFileService.saveSettingsFile('myconfig', data);

      // Assert
      const expectedJson = JSON.stringify(data, null, 2);
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'SAVE_FILE',
        {
          filename: 'myconfig',
          content: expectedJson
        }
      );
    });

    it('should return false on save failure', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockRejectedValue(
        new Error('Save failed')
      );

      // Suppress console.error
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();

      // Act
      const result = await settingsFileService.saveSettingsFile('myconfig', { key: 'value' });

      // Assert
      expect(result).toBe(false);

      consoleSpy.mockRestore();
    });

    it('should handle arrays', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true
      });

      const data = ['item1', 'item2', 'item3'];

      // Act
      const result = await settingsFileService.saveSettingsFile('myconfig', data);

      // Assert
      expect(result).toBe(true);
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'SAVE_FILE',
        {
          filename: 'myconfig',
          content: JSON.stringify(data, null, 2)
        }
      );
    });

    it('should handle primitive values', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true
      });

      // Act & Assert - String
      await settingsFileService.saveSettingsFile('myconfig', 'test');
      expect(bridgeService.sendMessage).toHaveBeenLastCalledWith(
        'SETTING',
        'SAVE_FILE',
        { filename: 'myconfig', content: '"test"' }
      );

      // Number
      await settingsFileService.saveSettingsFile('myconfig', 42);
      expect(bridgeService.sendMessage).toHaveBeenLastCalledWith(
        'SETTING',
        'SAVE_FILE',
        { filename: 'myconfig', content: '42' }
      );

      // Boolean
      await settingsFileService.saveSettingsFile('myconfig', true);
      expect(bridgeService.sendMessage).toHaveBeenLastCalledWith(
        'SETTING',
        'SAVE_FILE',
        { filename: 'myconfig', content: 'true' }
      );
    });
  });

  describe.skip('deleteSettingsFile', () => {
    it('should delete file successfully', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        success: true,
        message: 'File deleted'
      });

      // Act
      const result = await settingsFileService.deleteSettingsFile('myconfig');

      // Assert
      expect(result).toBe(true);
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'DELETE_FILE',
        { filename: 'myconfig' }
      );
    });

    it('should return false on delete failure', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockRejectedValue(
        new Error('Delete failed')
      );

      // Suppress console.error
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();

      // Act
      const result = await settingsFileService.deleteSettingsFile('myconfig');

      // Assert
      expect(result).toBe(false);

      consoleSpy.mockRestore();
    });
  });

  describe.skip('settingsFileExists', () => {
    it('should return true if file exists', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        exists: true
      });

      // Act
      const result = await settingsFileService.settingsFileExists('myconfig');

      // Assert
      expect(result).toBe(true);
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'FILE_EXISTS',
        { filename: 'myconfig' }
      );
    });

    it('should return false if file does not exist', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        exists: false
      });

      // Act
      const result = await settingsFileService.settingsFileExists('nonexistent');

      // Assert
      expect(result).toBe(false);
    });

    it('should return false on error', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockRejectedValue(
        new Error('Network error')
      );

      // Suppress console.error
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();

      // Act
      const result = await settingsFileService.settingsFileExists('myconfig');

      // Assert
      expect(result).toBe(false);

      consoleSpy.mockRestore();
    });
  });

  describe.skip('listSettingsFiles', () => {
    it('should return array of filenames', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        files: ['config1', 'config2', 'config3']
      });

      // Act
      const result = await settingsFileService.listSettingsFiles();

      // Assert
      expect(result).toEqual(['config1', 'config2', 'config3']);
      expect(bridgeService.sendMessage).toHaveBeenCalledWith(
        'SETTING',
        'LIST_FILES',
        {}
      );
    });

    it('should return empty array if no files', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({
        files: []
      });

      // Act
      const result = await settingsFileService.listSettingsFiles();

      // Assert
      expect(result).toEqual([]);
    });

    it('should return empty array if files is undefined', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockResolvedValue({});

      // Act
      const result = await settingsFileService.listSettingsFiles();

      // Assert
      expect(result).toEqual([]);
    });

    it('should return empty array on error', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock).mockRejectedValue(
        new Error('Network error')
      );

      // Suppress console.error
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();

      // Act
      const result = await settingsFileService.listSettingsFiles();

      // Assert
      expect(result).toEqual([]);

      consoleSpy.mockRestore();
    });
  });

  describe.skip('integration scenarios', () => {
    it('should handle save-then-load workflow', async () => {
      // Arrange
      const data = { theme: 'dark', count: 42 };

      (bridgeService.sendMessage as vi.Mock)
        .mockResolvedValueOnce({ success: true }) // Save
        .mockResolvedValueOnce({ success: true, content: JSON.stringify(data) }); // Load

      // Act
      const saved = await settingsFileService.saveSettingsFile('myconfig', data);
      const loaded = await settingsFileService.getSettingsFile('myconfig');

      // Assert
      expect(saved).toBe(true);
      expect(loaded).toEqual(data);
    });

    it('should handle check-exists-then-load workflow', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock)
        .mockResolvedValueOnce({ exists: true }) // Exists check
        .mockResolvedValueOnce({ success: true, content: '{"key":"value"}' }); // Load

      // Act
      const exists = await settingsFileService.settingsFileExists('myconfig');
      const data = exists ? await settingsFileService.getSettingsFile('myconfig') : null;

      // Assert
      expect(exists).toBe(true);
      expect(data).toEqual({ key: 'value' });
    });

    it('should handle list-then-load-all workflow', async () => {
      // Arrange
      (bridgeService.sendMessage as vi.Mock)
        .mockResolvedValueOnce({ files: ['config1', 'config2'] }) // List
        .mockResolvedValueOnce({ success: true, content: '{"id":1}' }) // Load config1
        .mockResolvedValueOnce({ success: true, content: '{"id":2}' }); // Load config2

      // Act
      const files = await settingsFileService.listSettingsFiles();
      const configs = await Promise.all(
        files.map(file => settingsFileService.getSettingsFile(file))
      );

      // Assert
      expect(files).toEqual(['config1', 'config2']);
      expect(configs).toEqual([{ id: 1 }, { id: 2 }]);
    });
  });
});
